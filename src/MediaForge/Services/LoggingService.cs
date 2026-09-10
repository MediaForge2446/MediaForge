using MediaForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaForge.Services;

public sealed class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Info(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _logger.LogInformation("{Message}", message);
    }

    public void Warning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _logger.LogWarning("{Message}", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "An unexpected error occurred.";
        }

        _logger.LogError(exception, "{Message}", message);
    }
}
