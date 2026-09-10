using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Current { get; }

    event EventHandler? Changed;

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
