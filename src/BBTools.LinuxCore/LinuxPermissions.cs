namespace BBTools.LinuxCore;

public static class LinuxPermissions
{
    public static string Scope()
    {
        try { return File.ReadAllText("/proc/sys/kernel/yama/ptrace_scope").Trim(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return "unavailable"; }
    }
    public static string Explain() => Explain(Scope());
    public static string Explain(string scope) => scope switch
    {
        "1" => "Linux denied access (Yama ptrace_scope=1). Launch the game from BB Tools so it is a child/descendant, using the same user and outside a sandbox. No system policy was changed.",
        "2" => "Linux denied access (Yama ptrace_scope=2: administrative tracing permission required). This tool does not change tracing permissions or elevate itself.",
        "3" => "Linux denied access (Yama ptrace_scope=3: process tracing is disabled). Launching as a child cannot bypass this system policy.",
        _ => $"Linux denied access (ptrace_scope={scope}). Check that both apps run as the same user outside Flatpak/Steam containers and that the game permits debugging. No system policy was changed."
    };
}
