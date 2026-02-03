using System.Runtime.InteropServices;
using System.Windows;

namespace CIDR.WPF;

/// <summary>
/// A retro 90s-styled About dialog displaying version, git, and runtime information
/// sourced from assembly metadata embedded at build time by the CI pipeline.
/// </summary>
public partial class AboutWindow : Window
{
    public string CopyrightText { get; } = $"\u00a9 {DateTime.UtcNow.Year} Andrew Stoltz. All rights reserved.";

    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;

        TxtVersion.Text = $"Version:    {BuildInfo.Version}";
        TxtCommit.Text = $"Commit:     {BuildInfo.GitCommit}";
        TxtBranch.Text = $"Branch/Tag: {BuildInfo.GitBranch}";
        TxtBuildDate.Text = $"Built:      {BuildInfo.BuildDate}";
        TxtRuntime.Text = $"Runtime:    {RuntimeInformation.FrameworkDescription}";
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
