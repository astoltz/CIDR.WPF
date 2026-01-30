using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace CIDR.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BtnCloudflare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnCloudflare.IsEnabled = false;
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

            TxtInput.Text = string.Join("\n", cidrs);
            ProcessAndSort();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error fetching Cloudflare IPs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnCloudflare.IsEnabled = true;
        }
    }

    private void BtnProcess_Click(object sender, RoutedEventArgs e)
    {
        ProcessAndSort();
    }

    private void CmbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            ProcessAndSort();
    }

    private void ProcessAndSort()
    {
        var input = TxtInput.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            TxtOutput.Text = "";
            return;
        }

        // Extract potential CIDRs using a regex that matches IP-like strings
        var matches = Regex.Matches(input, @"[0-9a-fA-F:.]+(?:/\d{1,3})?");
        var validCidrs = new List<CidrInfo>();

        foreach (Match match in matches)
        {
            var value = match.Value.TrimEnd('.', ':'); // Cleanup trailing punctuation if regex caught it
            
            if (IPAddress.TryParse(value, out var ip))
            {
                // It's just an IP, treat as /32 or /128
                validCidrs.Add(new CidrInfo(ip, ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
            }
            else
            {
                // Try parsing as CIDR
                var parts = value.Split('/');
                if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var ipPart) && int.TryParse(parts[1], out var prefix))
                {
                    validCidrs.Add(new CidrInfo(ipPart, prefix));
                }
            }
        }

        // Sort
        var sorted = validCidrs.OrderBy(c => c).ToList();

        // Format
        var formatIndex = CmbFormat.SelectedIndex;
        var sb = new StringBuilder();

        for (int i = 0; i < sorted.Count; i++)
        {
            var cidr = sorted[i];
            var cidrString = cidr.ToString();
            
            switch (formatIndex)
            {
                case 0: // One per line
                    sb.AppendLine(cidrString);
                    break;
                case 1: // Space separated
                    sb.Append(cidrString);
                    if (i < sorted.Count - 1) sb.Append(" ");
                    break;
                case 2: // F5
                    sb.AppendLine($"network {cidrString},");
                    break;
            }
        }

        TxtOutput.Text = sb.ToString();
    }

    private class CidrInfo : IComparable<CidrInfo>
    {
        public IPAddress Address { get; }
        public int PrefixLength { get; }

        public CidrInfo(IPAddress address, int prefixLength)
        {
            Address = address;
            PrefixLength = prefixLength;
        }

        public override string ToString()
        {
            return $"{Address}/{PrefixLength}";
        }

        public int CompareTo(CidrInfo? other)
        {
            if (other == null) return 1;
            
            // Compare Address Family first (IPv4 before IPv6)
            if (Address.AddressFamily != other.Address.AddressFamily)
            {
                return Address.AddressFamily.CompareTo(other.Address.AddressFamily);
            }

            // Compare Bytes
            var bytes1 = Address.GetAddressBytes();
            var bytes2 = other.Address.GetAddressBytes();

            // Should be same length if address family is same
            var len = Math.Min(bytes1.Length, bytes2.Length);
            for (int i = 0; i < len; i++)
            {
                if (bytes1[i] != bytes2[i])
                    return bytes1[i].CompareTo(bytes2[i]);
            }

            // Compare Prefix
            return PrefixLength.CompareTo(other.PrefixLength);
        }
    }
}
