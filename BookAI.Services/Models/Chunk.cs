using EpubCore;

namespace BookAI.Services.Models;

public record class Chunk
{
    public string Text { get; init; }
    
    /// <summary>
    /// Previous text
    /// </summary>
    public string? Context { get; init; }
    
    public EpubTextFile EpubTextFile { get; init; }
};