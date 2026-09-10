using System.Collections.ObjectModel;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.State;

namespace MediaForge.ViewModels;

public sealed class PendingChangesViewModel : ViewModelBase
{
    private readonly PendingChangesState _state;
    private readonly ICommitService _commitService;
    private readonly MainViewModel _main;

    public ObservableCollection<PendingChange> Changes { get; } = new();

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public bool CanSave => !IsSaving && Changes.Any(change => change.Status == ChangeStatus.Pending);

    public PendingChangesViewModel(
        PendingChangesState state,
        ICommitService commitService,
        MainViewModel main)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _commitService = commitService ?? throw new ArgumentNullException(nameof(commitService));
        _main = main ?? throw new ArgumentNullException(nameof(main));

        Refresh();
        _state.Changed += OnStateChanged;
    }

    public void Refresh()
    {
        Changes.Clear();
        foreach (var change in _state.Changes)
        {
            Changes.Add(change);
        }

        OnPropertyChanged(nameof(CanSave));
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave)
        {
            return;
        }

        try
        {
            IsSaving = true;
            var results = await _commitService.CommitAsync(Changes, cancellationToken).ConfigureAwait(true);
            _state.Replace(results);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _main.ReportError(new InvalidOperationException("Saving changes was canceled."));
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
        finally
        {
            IsSaving = false;
            Refresh();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
