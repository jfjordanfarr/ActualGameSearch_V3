using Microsoft.Extensions.AI;
using System.Linq;

namespace ActualGameSearch.Core.Embeddings;

public interface ITextEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateVectorAsync(string input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateVectorsAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default);
}

public sealed class TextEmbeddingService : ITextEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public TextEmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        _generator = generator;
    }

    public async Task<ReadOnlyMemory<float>> GenerateVectorAsync(string input, CancellationToken cancellationToken = default)
    {
        // Avoid extension methods to keep dependency on Abstractions only: call GenerateAsync and return first vector
        var result = await _generator.GenerateAsync(new[] { input }, cancellationToken: cancellationToken).ConfigureAwait(false);
        var first = result.FirstOrDefault();
        if (first is null)
        {
            return ReadOnlyMemory<float>.Empty;
        }
        return first.Vector;
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateVectorsAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default)
    {
        var items = inputs as IReadOnlyList<string> ?? inputs.ToList();
        var result = await _generator.GenerateAsync(items, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Select(e => e.Vector).ToList();
    }
}
