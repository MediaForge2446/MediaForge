using System.Text.Json.Serialization;
using MediaForge.Infrastructure.Persistence;

namespace MediaForge.App.Services;

public sealed class TermsAcceptanceService
{
    public const string CurrentTermsVersion = "1.0";

    private readonly AtomicJsonStore _store;
    private readonly LocalAppPaths _paths;

    public TermsAcceptanceService(AtomicJsonStore store, LocalAppPaths paths)
    {
        _store = store;
        _paths = paths;
    }

    public async Task<bool> HasAcceptedCurrentTermsAsync(CancellationToken cancellationToken = default)
    {
        var acceptance = await _store
            .LoadAsync<TermsAcceptance>(_paths.TermsAcceptanceFilePath, cancellationToken)
            .ConfigureAwait(false);

        return acceptance is
        {
            Accepted: true,
            Version: CurrentTermsVersion
        };
    }

    public Task AcceptCurrentTermsAsync(CancellationToken cancellationToken = default)
        => _store.SaveAsync(
            _paths.TermsAcceptanceFilePath,
            new TermsAcceptance(true, CurrentTermsVersion, DateTimeOffset.UtcNow),
            cancellationToken);

    private sealed record TermsAcceptance(
        bool Accepted,
        string Version,
        DateTimeOffset AcceptedAtUtc);
}
