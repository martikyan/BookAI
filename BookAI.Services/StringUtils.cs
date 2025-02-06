namespace BookAI.Services;

// todo: optimize
public static class StringUtils
{
    public static string Trim(this string str, string trimString)
    {
        return str.TrimStart(trimString).TrimEnd(trimString);
    }

    public static string TrimStart(this string str, string trimString)
    {
        if (str.StartsWith(trimString))
        {
            return str.Substring(trimString.Length);
        }

        return str;
    }

    public static string TrimEnd(this string str, string trimString)
    {
        if (str.EndsWith(trimString))
        {
            return str.Substring(0, str.Length - trimString.Length);
        }

        return str;
    }
}