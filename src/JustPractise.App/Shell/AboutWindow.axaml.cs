using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JustPractise.App.Shell;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // Die Lizenztexte sind lang; sie werden erst gelesen, wenn man sie sehen will.
        ShowLicenses.IsCheckedChanged += (_, _) =>
        {
            if (ShowLicenses.IsChecked == true && Licenses.Text is null && DataContext is AboutViewModel about)
            {
                Licenses.Text = about.LicenseText;
            }
        };
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
