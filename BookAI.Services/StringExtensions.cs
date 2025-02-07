namespace BookAI.Services;

public static class StringExtensions
{
    /// <summary>
    /// Trims all occurrences of the specified substring from the beginning and end of the input string.
    /// </summary>
    /// <param name="value">The string to trim.</param>
    /// <param name="trimString">The substring to remove from both ends of the string.</param>
    /// <returns>The trimmed string.</returns>
    public static string Trim(this string value, string trimString, StringComparison comparisonType = StringComparison.Ordinal)
    {
        // Trim occurrences from the beginning
        while (value.StartsWith(trimString, comparisonType))
        {
            value = value.Substring(trimString.Length);
        }

        // Trim occurrences from the end
        while (value.EndsWith(trimString, comparisonType))
        {
            value = value.Substring(0, value.Length - trimString.Length);
        }

        return value;
    }

    public static string Trim(this string value, StringComparison comparisonType = StringComparison.Ordinal, params string[] trimStrings)
    {
        foreach (var param in trimStrings)
        {
            value = value.Trim(param, comparisonType);
        }

        return value;
    }
}