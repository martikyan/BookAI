using BookAI.Services.Models;

namespace BookAI.Services.Abstraction;

public interface IAIService
{
    Task<ExplanationResponse> ExplainAsync(string sentence, Chunk chunk, CancellationToken cancellationToken);
    Task<ConfusionResponse> EvaluateConfusionAsync(Chunk chunk, CancellationToken cancellationToken);
    Task<EndnotesFixupResponse> FixupEndnotesAsync(string html, CancellationToken cancellationToken);
}