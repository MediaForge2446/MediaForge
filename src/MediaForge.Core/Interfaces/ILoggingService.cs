namespace MediaForge.Core.Interfaces;

public interface ILoggingService
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}
