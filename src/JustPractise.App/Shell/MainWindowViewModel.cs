using CommunityToolkit.Mvvm.ComponentModel;

namespace JustPractise.App.Shell;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public string Title { get; } = "just practise";
}
