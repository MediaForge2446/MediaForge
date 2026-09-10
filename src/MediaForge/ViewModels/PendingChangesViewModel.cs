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

    public ObservableCollection<PendingChangeItemViewModel> Changes { get; } = new();

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

    private double _overallProgress;
    public double OverallProgress
    {
        get => _overallProgress;
        private set => SetProperty(ref _overallProgress, Math.Clamp(value, 0d, 1d));
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
        var existingById = Changes.ToDictionary(item => item.Id);
        Changes.Clear();

        foreach (var change in _state.Changes)
        {
            if (existingById.TryGetValue(change.Id, out var existing))
            {
                existing.ApplyResult(change);
                Changes.Add(existing);
            }
            else
            {
                Changes.Add(new PendingChangeItemViewModel(change));
            }
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
            OverallProgress = 0d;

            var progress = new Progress<CommitProgress>(OnCommitProgress);
            var pending = _state.Changes;
            var results = await _commitService.CommitAsync(
                pending,
                progress,
                cancellationToken).ConfigureAwait(true);

            _state.Replace(results);
            OverallProgress = CalculateOverallProgress();
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

    private void OnCommitProgress(CommitProgress progress)
    {
        var item = Changes.FirstOrDefault(change => change.Id == progress.ChangeId);
        item?.ApplyCommitProgress(progress);

        var total = Math.Max(1, progress.TotalCount);
        OverallProgress = Math.Clamp(
            (progress.CompletedCount + progress.Progress) / total,
            0d,
            1d);
    }

    private double CalculateOverallProgress()
    {
        var active = Changes.Where(change => change.Status == ChangeStatus.Pending || change.Type == ChangeType.Download).ToArray();
        if (active.Length == 0)
        {
            return Changes.Count == 0 ? 0d : 1d;
        }

        return active.Average(change => change.Status == ChangeStatus.Synced ? 1d : change.Progress);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        Refresh();
    }
}
