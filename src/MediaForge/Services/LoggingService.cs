using System.Diagnostics;
using MediaForge.Core.Interfaces;

namespace MediaForge.Services;

public sealed class LoggingService : ILoggingService
{
    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warning(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        var suffix = exception is null ? string.Empty : $" | {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", $"{message}{suffix}");
    }

    private static void Write(string level, string message)
    {
        var safeMessage = string.IsNullOrWhiteSpace(message) ? "No message supplied." : message;
        Trace.WriteLine($"[{DateTimeOffset.UtcNow:O}] [{level}] {safeMessage}");
    }
}
