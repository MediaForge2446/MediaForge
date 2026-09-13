using System.Windows.Forms;

namespace MediaForge.App.Services;

public sealed class WindowsFolderPicker : IFolderPicker
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var dialog = new FolderBrowserDialog
        {
            Description = "בחר תיקיית ספרייה עבור MediaForge",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        return Task.FromResult<string?>(dialog.ShowDialog() == DialogResult.OK
            ? dialog.SelectedPath
            : null);
    }
}
