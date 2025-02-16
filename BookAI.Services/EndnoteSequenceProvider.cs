using System.Text;

namespace BookAI.Services;

public class EndnoteSequenceProvider
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

        var result = sb.ToString();

        return result;
    }
}