#if false // Temporarily disabled until zstd dependency and wiring are finalized
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZstdSharp;
using ZstdSharp.Port;

namespace ActualGameSearch.Worker.Storage;

public static class ExportImportHelper
{
    public static async Task<(string archivePath, string checksumSha256)> CreateTarZstAsync(string sourceDir, string outputDir, string archiveNameWithoutExt)
    {
        Directory.CreateDirectory(outputDir);
        var tarPath = Path.Combine(outputDir, archiveNameWithoutExt + ".tar");
        var zstPath = tarPath + ".zst";

        // Build a simple POSIX tar file (no PAX extensions) for portability
        using (var tarStream = File.Create(tarPath))
        {
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
                WriteTarEntry(tarStream, file, relative);
            }
            // two 512-byte blocks of zero indicate end of archive
            tarStream.Write(new byte[1024]);
        }

        // Compress with zstd
        using (var input = File.OpenRead(tarPath))
        using (var output = File.Create(zstPath))
        using (var compressor = new CompressionStream(output, 3))
        {
            await input.CopyToAsync(compressor);
        }

        // Compute checksum of the final archive
        using var sha = SHA256.Create();
        await using var zf = File.OpenRead(zstPath);
        var hash = await sha.ComputeHashAsync(zf);
        var checksumSha256 = Convert.ToHexString(hash).ToLowerInvariant();

        // Remove intermediate tar to save space
        try { File.Delete(tarPath); } catch { }
        return (zstPath, checksumSha256);
    }

    public static void ExtractTarZst(string archivePath, string destDir)
    {
        Directory.CreateDirectory(destDir);
        // Decompress to tar
        var tarPath = archivePath.EndsWith(".zst", StringComparison.OrdinalIgnoreCase)
            ? archivePath.Substring(0, archivePath.Length - 4)
            : archivePath + ".tar";
        using (var input = File.OpenRead(archivePath))
        using (var decompressor = new DecompressionStream(input))
        using (var tarOut = File.Create(tarPath))
        {
            decompressor.CopyTo(tarOut);
        }

        // Extract tar
        using (var tarStream = File.OpenRead(tarPath))
        {
            ReadTarToDirectory(tarStream, destDir);
        }
        try { File.Delete(tarPath); } catch { }
    }

    private static void WriteTarEntry(Stream tar, string filePath, string entryName)
    {
        var fi = new FileInfo(filePath);
        var header = new byte[512];
        WriteString(header, 0, entryName, 100);
        WriteOctal(header, 100, 8, 420.ToString()); // mode 0644
        WriteOctal(header, 108, 8, "0"); // uid
        WriteOctal(header, 116, 8, "0"); // gid
        WriteOctal(header, 124, 12, fi.Length.ToString());
        var mtime = ((DateTimeOffset)fi.LastWriteTimeUtc).ToUnixTimeSeconds();
        WriteOctal(header, 136, 12, mtime.ToString());
        header[156] = (byte)'0'; // type flag: regular file
        // magic + version
        WriteString(header, 257, "ustar", 6);
        WriteString(header, 263, "00", 2);
        // Compute checksum: treat checksum field as spaces
        for (int i = 148; i < 156; i++) header[i] = 0x20;
        var sum = header.Sum(b => (int)b);
        WriteOctal(header, 148, 8, sum.ToString());

        tar.Write(header);
        using (var fs = File.OpenRead(filePath))
        {
            fs.CopyTo(tar);
        }
        // pad to 512 block boundary
        var rem = fi.Length % 512;
        if (rem != 0)
        {
            var pad = 512 - (int)rem;
            tar.Write(new byte[pad]);
        }
    }

    private static void ReadTarToDirectory(Stream tar, string destDir)
    {
        while (true)
        {
            var header = new byte[512];
            var read = tar.Read(header, 0, 512);
            if (read == 0) break;
            if (read < 512) throw new InvalidDataException("Unexpected EOF in tar header");
            // check for two consecutive zero blocks (end of archive)
            var allZero = header.All(b => b == 0);
            if (allZero) break;

            var name = ReadString(header, 0, 100);
            var sizeOct = ReadString(header, 124, 12);
            if (!TryParseOctal(sizeOct, out long size)) size = 0;
            var typeFlag = header[156];
            // For simplicity, treat everything as regular file (type '0')
            var outPath = Path.Combine(destDir, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            if (typeFlag == (byte)'0' || typeFlag == 0)
            {
                using var outFs = File.Create(outPath);
                CopyExactly(tar, outFs, size);
            }
            else
            {
                // skip unknown types by consuming size bytes
                CopyExactly(tar, Stream.Null, size);
            }
            // skip padding
            var rem = size % 512;
            if (rem != 0)
            {
                var pad = 512 - (int)rem;
                tar.Read(new byte[pad], 0, pad);
            }
        }
    }

    private static void CopyExactly(Stream input, Stream output, long bytes)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long remaining = bytes;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(remaining, buffer.Length);
                int n = input.Read(buffer, 0, toRead);
                if (n <= 0) throw new EndOfStreamException();
                output.Write(buffer, 0, n);
                remaining -= n;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void WriteString(byte[] buf, int offset, string value, int maxLen)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        int len = Math.Min(bytes.Length, maxLen);
        Array.Copy(bytes, 0, buf, offset, len);
    }
    private static void WriteOctal(byte[] buf, int offset, int len, string value)
    {
        var v = Convert.ToUInt64(string.IsNullOrEmpty(value) ? "0" : value);
        var s = Convert.ToString((long)v, 8) ?? "0";
        s = s.PadLeft(len - 1, '0');
        WriteString(buf, offset, s + "\0", len);
    }
    private static string ReadString(byte[] buf, int offset, int len)
    {
        var span = new ReadOnlySpan<byte>(buf, offset, len);
        int i = 0; for (; i < span.Length; i++) if (span[i] == 0) break;
        return Encoding.ASCII.GetString(span.Slice(0, i)).Trim('\0', ' ');
    }
    private static bool TryParseOctal(string s, out long value)
    {
        value = 0;
        s = s.Trim('\0', ' ', '\t', '\r', '\n');
        if (s.Length == 0) return true;
        long v = 0;
        foreach (var ch in s)
        {
            if (ch < '0' || ch > '7') return false;
            v = (v << 3) + (ch - '0');
        }
        value = v;
        return true;
    }
}
#endif
