using MediaForge.State;

namespace MediaForge.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public AppState State { get; } = new();

    public LibraryViewModel Library { get; }

    private string? _lastError;
    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public MainViewModel()
    {
        Library = new LibraryViewModel(State.Library, this);
    }

    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LastError = exception.Message;
    }
}
