using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace CIDR.WPF;

/// <summary>
/// Main application window for CIDR Processor.
/// Parses, validates, sorts, and formats IPv4/IPv6 addresses and CIDR subnets.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Compiled regex for extracting potential IP addresses and CIDR notation from arbitrary text.
    /// Matches hex digits, dots, and colons (covering both IPv4 and IPv6) with an optional /prefix.
    /// </summary>
    private static readonly Regex CidrRegex = new(@"[0-9a-fA-F:.]+(?:/\d{1,3})?", RegexOptions.Compiled);

    private static readonly SolidColorBrush MatchedBrush = new(Color.FromRgb(0, 128, 0));   // Green
    private static readonly SolidColorBrush UnmatchedBrush = new(Color.FromRgb(180, 60, 60)); // Muted red

    private readonly HttpClient _httpClient = new();

    /// <summary>
    /// Prevents re-entrant processing when programmatically modifying the RichTextBox content
    /// (e.g. during highlight application or text insertion).
    /// </summary>
    private bool _suppressProcessing;

    public MainWindow()
    {
        InitializeComponent();

        // Wire up routed commands for standard File operations
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, OpenFile_Executed));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, SaveFile_Executed));

        Loaded += MainWindow_Loaded;
        TxtInput.TextChanged += TxtInput_TextChanged;
        TxtInput.GotFocus += (_, _) => UpdateWatermarkVisibility();
        TxtInput.LostFocus += (_, _) => UpdateWatermarkVisibility();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Show version in the bottom status bar
        TxtStatusBar.Text = $"CIDR Processor v{BuildInfo.Version}  |  Ready";

        // If text was set before the window was loaded (e.g. via command line args),
        // process it now that the UI is ready.
        if (!string.IsNullOrWhiteSpace(GetInputText()))
        {
            ProcessAndSort();
        }

        UpdateWatermarkVisibility();
    }

    // ────────────────────────────────────────────────────────────────
    //  RichTextBox helpers
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the plain-text content from the input RichTextBox.
    /// </summary>
    private string GetInputText()
    {
        var range = new TextRange(TxtInput.Document.ContentStart, TxtInput.Document.ContentEnd);
        return range.Text.TrimEnd();
    }

    /// <summary>
    /// Replaces all content in the input RichTextBox with the specified plain text,
    /// suppressing processing until the operation completes.
    /// </summary>
    private void SetInputText(string text)
    {
        _suppressProcessing = true;
        TxtInput.Document.Blocks.Clear();
        TxtInput.Document.Blocks.Add(new Paragraph(new Run(text)));
        _suppressProcessing = false;
    }

    // ────────────────────────────────────────────────────────────────
    //  Watermark
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the watermark placeholder when the input is empty and unfocused.
    /// </summary>
    private void UpdateWatermarkVisibility()
    {
        if (TxtInputWatermark != null)
        {
            TxtInputWatermark.Visibility =
                string.IsNullOrEmpty(GetInputText()) && !TxtInput.IsFocused
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Event handlers
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Auto-processes input whenever text changes (paste, type, drag-and-drop, or API import).
    /// </summary>
    private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateWatermarkVisibility();

        if (!_suppressProcessing && IsLoaded)
        {
            ProcessAndSort();
        }
    }

    /// <summary>
    /// Loads a file from disk into the input panel and triggers processing.
    /// File I/O is performed asynchronously to keep the UI responsive.
    /// </summary>
    public async Task LoadFileAsync(string filePath)
    {
        try
        {
            TxtStatusBar.Text = $"Loading: {Path.GetFileName(filePath)}...";
            var content = await File.ReadAllTextAsync(filePath);
            SetInputText(content);
            ProcessAndSort();
            TxtStatusBar.Text = $"Loaded: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            TxtStatusBar.Text = "File load failed";
            MessageBox.Show(
                $"Unable to read the selected file.\n\n{ex.Message}",
                "File Read Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void BtnCloudflare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnCloudflare.IsEnabled = false;
            BtnCloudflare.Content = "Retrieving...";
            TxtStatusBar.Text = "Contacting Cloudflare API...";

            var json = await _httpClient.GetStringAsync("https://api.cloudflare.com/client/v4/ips");
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            var cidrs = new List<string>();
            if (result.TryGetProperty("ipv4_cidrs", out var ipv4))
            {
                foreach (var item in ipv4.EnumerateArray()) cidrs.Add(item.GetString()!);
            }
            if (result.TryGetProperty("ipv6_cidrs", out var ipv6))
            {
                foreach (var item in ipv6.EnumerateArray()) cidrs.Add(item.GetString()!);
            }

            SetInputText(string.Join("\n", cidrs));
            ProcessAndSort();
            TxtStatusBar.Text = $"Cloudflare ranges imported ({cidrs.Count} entries)";
        }
        catch (Exception ex)
        {
            TxtStatusBar.Text = "Cloudflare import failed";
            MessageBox.Show(
                $"Unable to retrieve Cloudflare IP ranges.\n\n{ex.Message}",
                "Network Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            BtnCloudflare.IsEnabled = true;
            BtnCloudflare.Content = "_Import Cloudflare Ranges";
        }
    }

    private void BtnProcess_Click(object sender, RoutedEventArgs e)
    {
        ProcessAndSort();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private void CmbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            ProcessAndSort();
    }

    // ────────────────────────────────────────────────────────────────
    //  File operations (Ctrl+O, Ctrl+S)
    // ────────────────────────────────────────────────────────────────

    private void OpenFile_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Input File",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _ = LoadFileAsync(dialog.FileName);
        }
    }

    private async void SaveFile_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var output = TxtOutput.Text;
        if (string.IsNullOrWhiteSpace(output))
        {
            MessageBox.Show(
                "There is no output to save. Process input data first.",
                "Nothing to Save",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save Output As",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = "cidr-output.txt"
        };

        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                TxtStatusBar.Text = $"Saving: {Path.GetFileName(dialog.FileName)}...";
                await File.WriteAllTextAsync(dialog.FileName, output);
                TxtStatusBar.Text = $"Saved: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = "File save failed";
                MessageBox.Show(
                    $"Unable to save the file.\n\n{ex.Message}",
                    "File Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Drag and drop
    // ────────────────────────────────────────────────────────────────

    private void Window_Drop(object sender, DragEventArgs e)
    {
        HandleDrop(e);
    }

    private void TxtInput_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
    }

    private void TxtInput_Drop(object sender, DragEventArgs e)
    {
        HandleDrop(e);
    }

    private void HandleDrop(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files is { Length: > 0 })
            {
                _ = LoadFileAsync(files[0]);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Core processing logic
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the input text for valid IP addresses and CIDR subnets,
    /// sorts them (IPv4 first, then IPv6, numerically within each family),
    /// applies syntax highlighting to the input, and writes the formatted output.
    /// </summary>
    private void ProcessAndSort()
    {
        var input = GetInputText();
        if (string.IsNullOrWhiteSpace(input))
        {
            TxtOutput.Text = "";
            TxtStatus.Text = "Enter or paste IP addresses and CIDR subnets to begin.";
            TxtStatus.Foreground = Brushes.Gray;
            return;
        }

        var matches = CidrRegex.Matches(input);
        var validCidrs = new List<CidrInfo>();

        // Track which character ranges in the input are valid matches for highlighting
        var matchedRanges = new List<(int Start, int End)>();

        foreach (Match match in matches)
        {
            var value = match.Value.TrimEnd('.', ':');

            if (IPAddress.TryParse(value, out var ip))
            {
                validCidrs.Add(new CidrInfo(ip,
                    ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
                matchedRanges.Add((match.Index, match.Index + match.Length));
            }
            else
            {
                var parts = value.Split('/');
                if (parts.Length == 2
                    && IPAddress.TryParse(parts[0], out var ipPart)
                    && int.TryParse(parts[1], out var prefix))
                {
                    validCidrs.Add(new CidrInfo(ipPart, prefix));
                    matchedRanges.Add((match.Index, match.Index + match.Length));
                }
            }
        }

        // Apply syntax highlighting to the input RichTextBox
        ApplyHighlighting(input, matchedRanges);

        // Sort: IPv4 before IPv6, then numerically by address bytes, then by prefix length
        var sorted = validCidrs.OrderBy(c => c).ToList();

        // Update status bar with detection summary
        var ipv4Count = sorted.Count(c => c.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        var ipv6Count = sorted.Count - ipv4Count;
        TxtStatus.Text = $"Detected {sorted.Count} valid {(sorted.Count == 1 ? "entry" : "entries")} " +
                          $"({ipv4Count} IPv4, {ipv6Count} IPv6)";
        TxtStatus.Foreground = sorted.Count > 0 ? MatchedBrush : UnmatchedBrush;

        // Format output according to the selected format
        var formatIndex = CmbFormat.SelectedIndex;
        var sb = new StringBuilder();

        for (var i = 0; i < sorted.Count; i++)
        {
            var cidrString = sorted[i].ToString();

            switch (formatIndex)
            {
                case 0: // One Per Line
                    sb.AppendLine(cidrString);
                    break;
                case 1: // Space-Delimited
                    sb.Append(cidrString);
                    if (i < sorted.Count - 1) sb.Append(' ');
                    break;
                case 2: // F5 BigIP Format
                    sb.AppendLine($"network {cidrString},");
                    break;
                case 3: // Nginx Allow
                    sb.AppendLine($"allow {cidrString};");
                    break;
                case 4: // Apache Require
                    sb.AppendLine($"Require ip {cidrString}");
                    break;
            }
        }

        TxtOutput.Text = sb.ToString();
    }

    /// <summary>
    /// Applies green/red syntax highlighting to the input RichTextBox content.
    /// Matched (valid) IP/CIDR tokens are colored green; all other text is colored red
    /// to clearly indicate what the parser did not recognize.
    /// </summary>
    private void ApplyHighlighting(string input, List<(int Start, int End)> matchedRanges)
    {
        _suppressProcessing = true;

        try
        {
            // Rebuild the document with colored Run elements for each segment
            var paragraph = new Paragraph();
            var lastEnd = 0;

            foreach (var (start, end) in matchedRanges)
            {
                // Unmatched text before this match (red)
                if (start > lastEnd)
                {
                    var unmatchedText = input[lastEnd..start];
                    paragraph.Inlines.Add(new Run(unmatchedText) { Foreground = UnmatchedBrush });
                }

                // Matched CIDR/IP text (green)
                var matchedText = input[start..end];
                paragraph.Inlines.Add(new Run(matchedText) { Foreground = MatchedBrush });

                lastEnd = end;
            }

            // Remaining unmatched text after the last match (red)
            if (lastEnd < input.Length)
            {
                var remaining = input[lastEnd..];
                paragraph.Inlines.Add(new Run(remaining) { Foreground = UnmatchedBrush });
            }

            TxtInput.Document.Blocks.Clear();
            TxtInput.Document.Blocks.Add(paragraph);

            // Restore caret to the end so the user can continue typing
            TxtInput.CaretPosition = TxtInput.Document.ContentEnd;
        }
        finally
        {
            _suppressProcessing = false;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  CidrInfo: represents a parsed IP address or CIDR subnet
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a single IP address with its CIDR prefix length.
    /// Implements <see cref="IComparable{CidrInfo}"/> so entries can be sorted
    /// by address family (IPv4 first), then numerically by address bytes,
    /// and finally by prefix length.
    /// </summary>
    private class CidrInfo : IComparable<CidrInfo>
    {
        public IPAddress Address { get; }
        public int PrefixLength { get; }

        public CidrInfo(IPAddress address, int prefixLength)
        {
            Address = address;
            PrefixLength = prefixLength;
        }

        public override string ToString() => $"{Address}/{PrefixLength}";

        public int CompareTo(CidrInfo? other)
        {
            if (other == null) return 1;

            // IPv4 (InterNetwork) sorts before IPv6 (InterNetworkV6)
            if (Address.AddressFamily != other.Address.AddressFamily)
                return Address.AddressFamily.CompareTo(other.Address.AddressFamily);

            // Compare address bytes numerically
            var bytes1 = Address.GetAddressBytes();
            var bytes2 = other.Address.GetAddressBytes();
            var len = Math.Min(bytes1.Length, bytes2.Length);

            for (var i = 0; i < len; i++)
            {
                if (bytes1[i] != bytes2[i])
                    return bytes1[i].CompareTo(bytes2[i]);
            }

            // Same address — sort by prefix length (narrower first)
            return PrefixLength.CompareTo(other.PrefixLength);
        }
    }
}
