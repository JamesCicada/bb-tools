using System.Text.RegularExpressions;
namespace BBTools;
public static class Diagnostics
{
    public static string RedactPaths(string text) => Regex.Replace(text,@"(?i)(?:[a-z]:[\\/]|\\\\)[^\r\n]*","[path omitted]");
}
