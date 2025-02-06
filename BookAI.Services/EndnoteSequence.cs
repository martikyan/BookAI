using System.Text;
using Microsoft.Extensions.Logging;

namespace BookAI.Services;

public class EndnoteSequence(ILogger<EndnoteSequence> logger)
{
    private readonly char[] _symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private int _iterator = 0;

    public string GetNext()
    {
        var sb = new StringBuilder();
        var current = ++_iterator;
        while (current > 0)
        {
            current--;
            var charIndex = current % _symbols.Length;
            current = current / _symbols.Length;
            sb.Insert(0, _symbols[charIndex]);
        }

        sb.Append("AI");
        var result = sb.ToString();
        logger.LogDebug("Generated endnote refrerence sequence {Sequence}", result);

        return result;
    }
}