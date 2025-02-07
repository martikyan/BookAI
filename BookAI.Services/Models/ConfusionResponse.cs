namespace BookAI.Services.Models;

public class ConfusionResponse
{
    public TextConfusionScore[] TextConfusionScores { get; init; }
}

public class TextConfusionScore
{
    public string Text { get; init; }
    public int ConfusionScore { get; init; }
}