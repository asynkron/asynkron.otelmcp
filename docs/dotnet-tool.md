# `dotnet-otelmcp` Global Tool

The receiver project can be distributed as a .NET global tool named `dotnet-otelmcp`. Installing the tool provides the `dotnet otelmcp` command that boots the OTLP receiver and applies EF Core migrations.

## Installation

Install from NuGet once the package is published:

```bash
# Installs the published package from nuget.org
dotnet tool install --global dotnet-otelmcp
```

During local development you can install directly from a packed `.nupkg` folder:

```bash
# Replace ./nupkg with the folder produced by `dotnet pack`
dotnet tool install --global --add-source ./nupkg dotnet-otelmcp
```

Ensure that the global tool path is on your `PATH` (typically `~/.dotnet/tools`).

## Running the Receiver

Starting the server uses the default `appsettings.json` connection strings bundled with the tool. On first launch the Entity Framework Core migrations execute automatically to create or upgrade the backing database.

```bash
# Launch the OTLP receiver (the global tool shim is also exposed as `otelmcp`)
dotnet otelmcp
```

> **Note:** The `dotnet` driver resolves to the same shim the global tool installs. If your shell cannot locate the command via `dotnet otelmcp`, invoke `otelmcp` directly.

## Configuration

The receiver is a standard ASP.NET Core application and can be configured using any of the standard configuration methods:

### Using Environment Variables

```bash
# Set the bind address using environment variables
export ASPNETCORE_URLS="http://0.0.0.0:7171"
dotnet otelmcp
```

### Using Command-Line Arguments

```bash
# Bind to all interfaces on port 7171 using command-line arguments
dotnet otelmcp --urls "http://0.0.0.0:7171"
```

### Using appsettings.json

You can also create an `appsettings.json` file in the working directory with custom configuration:

```json
{
  "Urls": "http://0.0.0.0:7171",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=custom-otel.db"
  }
}
```

See the [ASP.NET Core Configuration documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/) for more details on available configuration options.
