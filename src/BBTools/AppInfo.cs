namespace BBTools;

public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/JamesCicada/bb-tools";
    public static Version Version { get; } = new(typeof(AppInfo).Assembly.GetName().Version!.ToString(3));
    public static string DisplayVersion => Version.ToString(3);
}
