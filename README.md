# CIDR.WPF

A simple WPF utility for processing, sorting, and formatting CIDR subnets and IP addresses.

## Features

*   **CIDR/IP Parsing**: Extracts valid IPv4 and IPv6 addresses and CIDR subnets from any text input.
*   **Sorting**: Automatically sorts IP addresses numerically (IPv4 first, then IPv6).
*   **Cloudflare Integration**: Quickly fetch the latest Cloudflare IP ranges directly from their API.
*   **Flexible Output Formats**:
    *   **One per line**: Standard list format.
    *   **Space separated**: Useful for command-line arguments or scripts.
    *   **F5**: Formatted for F5 BigIP configuration (`network <cidr>,`).

## Getting Started

### Prerequisites

*   Windows OS (WPF application)
*   .NET 10.0 Runtime (or SDK to build)

### Usage

1.  **Input Data**:
    *   Paste text containing IPs or subnets into the "Input" box.
    *   Or, click **"Fetch Cloudflare IPs"** to automatically populate the input with Cloudflare's current IP ranges.
2.  **Select Format**: Choose your desired output format from the dropdown menu.
3.  **Process**: Click **"Process & Sort"** (or just change the format if data is already loaded).
4.  **Result**: The sorted and formatted list will appear in the "Output" box.

## Building the Project

This project uses .NET 10.0.

```powershell
dotnet restore
dotnet build
```

## CI/CD

The project includes a GitHub Actions workflow (`.github/workflows/dotnet.yml`) that:

1.  Builds the application for Windows x64 (Debug and Release).
2.  Publishes a self-contained single-file executable.
3.  Uploads artifacts to GitHub.
4.  Deploys the artifacts to an Octopus Deploy server (requires configuration of secrets and variables).
