# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NRuuviTag is a .NET library collection for interacting with RuuviTag IoT sensors via Bluetooth LE. It provides platform-specific listeners (Windows/Linux), publishing agents (MQTT/Azure Event Hubs), and command-line tools for sensor data collection and forwarding.

The full README is available [here](README.md)

## Build System

The project uses Cake build automation with cross-platform support:

- **Build script**: `build.cake` (run via `build.ps1` or `build.sh`)
- **Default target**: `Test` (builds, runs tests)
- **Key targets**: `Clean`, `Restore`, `Build`, `Test`, `Pack`, `Publish`, `PublishContainer`
- **Solution file**: `NRuuviTag.slnx` (SLNX format)

### Common Build Commands

```bash
# Default build and test
./build.sh

# Build specific target
./build.sh --target=Build

# Release build
./build.sh --configuration=Release

# Clean build
./build.sh --clean

# Skip tests
./build.sh --no-tests

# Container build (Linux CLI only)
./build.sh --target=PublishContainer
```

## Package Management

The project uses NuGet Central Package Management to manage dependencies. The `Directory.Packages.props` file defines common package versions used across the solution.


## Code Style

The project uses an [EditorConfig](.editorconfig) file to enforce consistent coding styles across the solution. Follow the rules defined in this file when contributing code.

The following subsections describe some additional conventions that should also be observed:

### Control Statements

Control statements such as `if` and `for` should always use braces, even for single line statements, to improve readability and maintainability.


### Async/Await

When generating an `await` statement in a class library project, always include `.ConfigureAwait(false)`. Is it not necessary to do this in ASP.NET Core projects, console applications, or unit test or benchmarking projects.


### Cancellation Tokens

`CancellationToken` parameters should be named `cancellationToken`. Do not suggest a default value for `cancellationToken` parameters in methods that are not `public` unless the method contains other parameters with default values.


### XML DocComments

When generating XML DocComments always use `<see langword="null"/>` instead of `null` or `<c>null</c>` when referring to the `null` C# language keyword.

The preferred summary for a constructor is `Creates a new <see cref="TypeName"/> instance.`.

The preferred text for a `CancellationToken` parameter is `The cancellation token for the operation.`.

When generating `<exception>` tags for `ArgumentNullException`, a separate tag should be generated for each parameter than can cause the exception. The preferred text is `<paramref name="parameterName"/> is <see langword="null"/>.`.


## Architecture

### Core Components

1. **NRuuviTag.Core** - Base types and interfaces
    - `IRuuviTagListener` - Main abstraction for BLE listeners
    - `RuuviTagListener` - Base implementation with telemetry
    - `RuuviTagSample` - Data structures for sensor readings

2. **Platform Listeners**
    - `NRuuviTag.Listener.Windows` - Windows 10 SDK implementation
    - `NRuuviTag.Listener.Linux` - BlueZ D-Bus implementation via Linux.Bluetooth package

3. **Publishing Agents**
    - `NRuuviTag.Mqtt.Agent` - MQTT publishing with configurable options
    - `NRuuviTag.AzureEventHubs.Agent` - Azure Event Hub publishing

4. **Command-Line Tools**
    - `NRuuviTag.Cli` - Common CLI framework using Spectre.Console
    - `NRuuviTag.Cli.Windows` - Windows-specific CLI executable
    - `NRuuviTag.Cli.Linux` - Linux-specific CLI executable

### Key Patterns

- **Async enumerable pattern**: All listeners use `IAsyncEnumerable<RuuviTagSample>`
- **Dependency injection**: Uses Microsoft.Extensions.DependencyInjection
- **OpenTelemetry**: Built-in metrics and logging instrumentation
- **Configuration**: Standard .NET configuration with appsettings.json

## Testing

Test projects use MSTest framework:
- `NRuuviTag.Core.Tests` - Core functionality tests
- `NRuuviTag.Mqtt.Tests` - MQTT publishing tests

Run tests via build script or directly:
```bash
dotnet test
```

## Container Support

Linux CLI can be containerized:
- Container project: `NRuuviTag.Cli.Linux`
- Runs as root for BlueZ access
- See `docs/Docker.md` for details

## Linux Service

The CLI can run as a systemd service:
- Service file: `src/NRuuviTag.Cli.Linux/nruuvitag.service`
- See `docs/LinuxSystemdService.md` for setup

## Project Structure

- `src/` - Main source code libraries and applications
- `src/submodules/` - DotNet-BlueZ submodule for Linux BLE support
- `samples/` - Example usage projects
- `test/` - Unit test projects
- `docs/` - Additional documentation
- `build/` - Build configuration (version.json)
- `tools/` - Build tools and extensions

## Version Management

Version information is stored in `build/version.json` and used by the build system for assembly versioning and NuGet package generation.