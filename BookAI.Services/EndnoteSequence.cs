using System.Text;

namespace BookAI.Services;

public class EndnoteSequence
{
    private int _iterator = 0;

    public string GetNext()
    {
        var bytes = BitConverter.GetBytes(++_iterator);
        return Convert.ToBase64String(bytes).TrimEnd('=').TrimEnd('A');
    }
}