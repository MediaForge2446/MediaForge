using MediaForge.Core.Enums;
using MediaForge.Core.Models;

namespace MediaForge.ViewModels;

public sealed class PendingChangeItemViewModel : ViewModelBase
{
    private double _progress;
    private string? _statusMessage;
    private bool _isRetrying;
    private int _retryAttempt;
    private ChangeStatus _status;
    private string? _errorMessage;

    public Guid Id { get; }
    public ChangeType Type { get; }
    public string SourcePath { get; }
    public string? TargetPath { get; }

    public ChangeStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, Math.Clamp(value, 0d, 1d));
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsRetrying
    {
        get => _isRetrying;
        private set => SetProperty(ref _isRetrying, value);
    }

    public int RetryAttempt
    {
        get => _retryAttempt;
        private set => SetProperty(ref _retryAttempt, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool ShowProgress => Type == ChangeType.Download && Status == ChangeStatus.Pending;

    public string StatusText => Status switch
    {
        ChangeStatus.Synced => "Saved",
        ChangeStatus.Failed => string.IsNullOrWhiteSpace(ErrorMessage) ? "Failed" : ErrorMessage,
        _ => IsRetrying && RetryAttempt > 1
            ? $"Retrying • attempt {RetryAttempt}"
            : StatusMessage ?? "Waiting to save"
    };

    public PendingChangeItemViewModel(PendingChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        Id = change.Id;
        Type = change.Type;
        SourcePath = change.SourcePath;
        TargetPath = change.TargetPath;
        _status = change.Status;
        _errorMessage = change.ErrorMessage;
        _statusMessage = change.Status == ChangeStatus.Failed ? "Failed" : "Waiting to save";
    }

    public void ApplyCommitProgress(CommitProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        if (progress.ChangeId != Id)
        {
            return;
        }

        Progress = progress.Progress;
        StatusMessage = progress.Message;
        IsRetrying = progress.IsRetrying;
        RetryAttempt = progress.RetryAttempt;

        if (progress.Error is not null)
        {
            ErrorMessage = progress.Error.Message;
        }

        if (progress.IsRetrying)
        {
            Status = ChangeStatus.Pending;
        }

        RaiseStatusNotifications();
    }

    public void ApplyResult(PendingChange result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Id != Id)
        {
            return;
        }

        Status = result.Status;
        ErrorMessage = result.ErrorMessage;
        if (result.Status == ChangeStatus.Synced)
        {
            Progress = 1d;
            StatusMessage = "Saved successfully";
            IsRetrying = false;
        }
        else if (result.Status == ChangeStatus.Failed)
        {
            StatusMessage = "Save failed";
            IsRetrying = false;
        }

        RaiseStatusNotifications();
    }

    private void RaiseStatusNotifications()
    {
        OnPropertyChanged(nameof(ShowProgress));
        OnPropertyChanged(nameof(StatusText));
    }
}
