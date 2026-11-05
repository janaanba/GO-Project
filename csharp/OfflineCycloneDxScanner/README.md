 # Offline CycloneDX Scanner

This project scans Conan, Dart, Composer, Gradle, and Maven lock or project
files in offline mode and writes a CycloneDX JSON report.

## Directory structure

- `Core/Constants` contains shared constants.
- `Core/Contracts` contains interfaces and ports.
- `Core/Domain` contains scan options, dependency graph, and report models.
- `Core/Helpers` contains parsing and filesystem helpers.
- `Core/Sbom` contains SBOM and CycloneDX model records.
- `Application` contains orchestration, encoding, writing, and the CLI entry point.
- `Analyzers` contains one folder per supported ecosystem.
- `Infrastructure` contains filesystem, clock, diagnostics, and identifier implementations.
- `tests` contains the executable regression suite and parity comparison script.

## Run

```zsh
dotnet run --project OfflineCycloneDxScanner.csproj -- <input> --format cyclonedx --offline-scan
```

## Test

```zsh
dotnet run --project tests/OfflineCycloneDxScanner.Tests.csproj --no-restore
```
