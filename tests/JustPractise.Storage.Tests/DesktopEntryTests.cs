namespace JustPractise.Storage.Tests;

public sealed class DesktopEntryTests : IDisposable
{
    private static readonly byte[] Icon = [1, 2, 3];
    private static readonly byte[] NewIcon = [4, 5, 6];

    private readonly TemporaryDirectory _home = new();

    public void Dispose() => _home.Dispose();

    [Fact]
    public void WritesTheEntryAndTheIcon()
    {
        var entry = new DesktopEntry(_home.Path);

        Assert.True(entry.Update("/home/user/Apps/just-practise.AppImage", Icon));

        var content = File.ReadAllText(entry.EntryPath);
        Assert.Contains("Exec=\"/home/user/Apps/just-practise.AppImage\"\n", content, StringComparison.Ordinal);
        Assert.Contains("TryExec=/home/user/Apps/just-practise.AppImage\n", content, StringComparison.Ordinal);
        Assert.Contains("StartupWMClass=just-practise\n", content, StringComparison.Ordinal);
        var icon = Assert.Single(Directory.GetFiles(entry.IconDirectory));
        Assert.Contains($"Icon={Path.GetFileNameWithoutExtension(icon)}\n", content, StringComparison.Ordinal);
        Assert.Matches("^just-practise-[0-9a-f]{8}\\.png$", Path.GetFileName(icon));
        Assert.Equal(Icon, File.ReadAllBytes(icon));
    }

    [Fact]
    public void WritesNothingWhenNothingChanged()
    {
        var entry = new DesktopEntry(_home.Path);
        entry.Update("/opt/just-practise.AppImage", Icon);

        Assert.False(entry.Update("/opt/just-practise.AppImage", Icon));
    }

    [Fact]
    public void FollowsAMovedAppImage()
    {
        var entry = new DesktopEntry(_home.Path);
        entry.Update("/opt/just-practise.AppImage", Icon);

        Assert.True(entry.Update("/home/user/just-practise.AppImage", Icon));

        Assert.Contains("Exec=\"/home/user/just-practise.AppImage\"", File.ReadAllText(entry.EntryPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ReplacesAChangedIconUnderANewName()
    {
        var entry = new DesktopEntry(_home.Path);
        entry.Update("/opt/just-practise.AppImage", Icon);
        var oldIcon = Assert.Single(Directory.GetFiles(entry.IconDirectory));
        var unrelated = Path.Combine(entry.IconDirectory, "just-practise-custom.png");
        File.WriteAllBytes(unrelated, Icon);

        Assert.True(entry.Update("/opt/just-practise.AppImage", NewIcon));

        var newIcon = Path.Combine(entry.IconDirectory, Path.GetFileName(Directory.GetFiles(entry.IconDirectory).Single(path => path != unrelated)));
        Assert.NotEqual(oldIcon, newIcon);
        Assert.False(File.Exists(oldIcon));
        Assert.True(File.Exists(unrelated));
        Assert.Equal(NewIcon, File.ReadAllBytes(newIcon));
        Assert.Contains($"Icon={Path.GetFileNameWithoutExtension(newIcon)}\n", File.ReadAllText(entry.EntryPath), StringComparison.Ordinal);
    }

    [Fact]
    public void EscapesSpecialCharactersInThePath()
    {
        var content = DesktopEntry.Content("/home/user/My \"Apps\"/100% $x\\y.AppImage", "icon");

        Assert.Contains("Exec=\"/home/user/My \\\\\"Apps\\\\\"/100%% \\\\$x\\\\\\\\y.AppImage\"\n", content, StringComparison.Ordinal);
        Assert.Contains("TryExec=/home/user/My \"Apps\"/100% $x\\\\y.AppImage\n", content, StringComparison.Ordinal);
    }
}
