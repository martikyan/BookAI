using EpubCore;

namespace BookAI.Services.Models;

public record Chunk
{
    public string Text { get; init; }

    public string? Context { get; init; }

    public EpubTextFile EpubTextFile { get; init; }
}