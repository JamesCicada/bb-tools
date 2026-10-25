using System.ComponentModel;

namespace BBTools.Memory;

public static class ProcessAccessErrors
{
    public static bool IsAccessDenied(Exception error)
    {
        for (Exception? current = error; current != null; current = current.InnerException)
            if (current is Win32Exception { NativeErrorCode: 5 } or UnauthorizedAccessException) return true;
        return false;
    }
}
