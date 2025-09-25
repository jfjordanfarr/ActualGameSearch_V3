using System.Collections.Generic;
using System.Text.Json;

namespace ActualGameSearch.Worker.Processing;

public static class ReviewSanitizer
{
    // PII keys to remove from review JSON documents
    private static readonly HashSet<string> PiiKeys = new(new[]
    {
        "author",          // nested author object contains steamid and profile
        "username",
        "profile",
        "steamid",
        "user"
    }, System.StringComparer.OrdinalIgnoreCase);

    public static JsonElement Sanitize(JsonElement root)
    {
        using var doc = JsonDocument.Parse(SanitizeToString(root));
        return doc.RootElement.Clone();
    }

    private static string SanitizeToString(JsonElement root)
    {
        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            if (root.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var prop in root.EnumerateObject())
                {
                    if (PiiKeys.Contains(prop.Name))
                        continue;

                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        // Drop nested objects fully if they are known PII containers (e.g., author)
                        if (PiiKeys.Contains(prop.Name))
                            continue;

                        writer.WritePropertyName(prop.Name);
                        writer.WriteStartObject();
                        foreach (var sub in prop.Value.EnumerateObject())
                        {
                            if (PiiKeys.Contains(sub.Name))
                                continue;
                            sub.WriteTo(writer);
                        }
                        writer.WriteEndObject();
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }
            else
            {
                // Non-object roots are passed through unchanged
                root.WriteTo(writer);
            }
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
