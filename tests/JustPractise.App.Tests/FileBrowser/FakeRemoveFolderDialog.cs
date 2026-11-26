using JustPractise.App.FileBrowser;

namespace JustPractise.App.Tests.FileBrowser;

/// <summary>Antwortet auf die Rückfrage beim Entfernen mit <see cref="Answer"/> und merkt sich, was gefragt wurde.</summary>
internal sealed class FakeRemoveFolderDialog : IRemoveFolderDialog
{
    public RemoveFolderChoice Answer { get; set; } = RemoveFolderChoice.Remove;

    public List<(string FolderName, int SidecarCount)> Questions { get; } = [];

    public Task<RemoveFolderChoice> AskAsync(string folderName, int sidecarCount)
    {
        Questions.Add((folderName, sidecarCount));
        return Task.FromResult(Answer);
    }
}
