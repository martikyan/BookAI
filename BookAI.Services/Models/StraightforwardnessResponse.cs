namespace BookAI.Services.Models;

public class StraightforwardnessResponse
{
    public SentenceStraightforwardness[] SentenceRatings { get; init; }
}

public class SentenceStraightforwardness
{
    public string Sentence { get; init; }
    public int Straightforwardness { get; init; }
    public string? Explanation { get; init; }
}