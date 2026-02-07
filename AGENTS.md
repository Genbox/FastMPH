# AGENTS.md - FastMPH Agent Guide

This file is for coding agents working in this repository.
It documents build/test/lint workflows and code-style constraints.

## Repository Snapshot

- Solution: `FastMPH.slnx`
- Library: `Src/FastMPH/FastMPH.csproj`
- Tests: `Src/FastMPH.Tests/FastMPH.Tests.csproj`
- Benchmarks: `Src/FastMPH.Benchmarks/FastMPH.Benchmarks.csproj`
- Example app: `Src/FastMPH.Examples/FastMPH.Examples.csproj`
- Target framework: `net10.0`
- Nullable: enabled
- LangVersion: latest

## Toolchain

- Use .NET SDK 10.x.
- Run commands from repo root.
- CI build uses: `dotnet build -c Release FastMPH.slnx`.

## Build Commands

- Restore: `dotnet restore FastMPH.slnx`
- Build (Debug): `dotnet build FastMPH.slnx`
- Build (Release / CI-like): `dotnet build -c Release FastMPH.slnx`
- Build one project: `dotnet build Src/FastMPH/FastMPH.csproj`

## Test Commands

- Run all tests:
  - `dotnet test Src/FastMPH.Tests/FastMPH.Tests.csproj`
- Run all tests in Release without restore:
  - `dotnet test -c Release --no-restore Src/FastMPH.Tests/FastMPH.Tests.csproj`
- Run a single test method:
  - `dotnet test Src/FastMPH.Tests/FastMPH.Tests.csproj --filter "FullyQualifiedName~Genbox.FastMPH.Tests.HashTests.PerfectHashTest"`
- Run tests by partial name:
  - `dotnet test Src/FastMPH.Tests/FastMPH.Tests.csproj --filter "Name~PerfectHash"`

Agent notes:
- Test framework is xUnit.
- Prefer `--filter` for single-test execution.
- Run full test project before finalizing functional changes.

## Lint / Analyzer Commands

Important project behavior:
- `RunAnalyzersDuringBuild` is set to `false` in `Src/Directory.Build.props`.
- IDE live analysis is on, but plain CLI build does not lint unless overridden.

Use for linting:
- `dotnet build FastMPH.slnx -p:RunAnalyzersDuringBuild=true`

Optional formatting check:
- `dotnet format --verify-no-changes FastMPH.slnx`

## Pack / Release Commands

- Pack NuGet artifacts:
  - `dotnet pack -c Release -o tmp FastMPH.slnx`
- Push package (release workflow usage):
  - `dotnet nuget push --skip-duplicate -k <NUGET_KEY> -s https://api.nuget.org/v3/index.json tmp/*`

## Benchmark / Example Commands

- Run benchmarks:
  - `dotnet run -c Release --project Src/FastMPH.Benchmarks/FastMPH.Benchmarks.csproj`
- Run example app:
  - `dotnet run --project Src/FastMPH.Examples/FastMPH.Examples.csproj`

## Configuration Sources of Truth

- Formatting and style: `.editorconfig`
- Analyzer severities: `.globalconfig`
- Shared build props: `Src/Directory.Build.props`
- Shared build targets: `Src/Directory.Build.targets`
- Package versions: `Src/Directory.Packages.props`
- Analyzer package versions: `Src/Directory.Packages.Analyzers.props`

## Cursor / Copilot Rules

Checked paths:
- `.cursorrules`
- `.cursor/rules/`
- `.github/copilot-instructions.md`

Current state:
- No Cursor rules were found.
- No Copilot instructions were found.

If these files are later added:
- Treat them as higher-priority instructions.
- Keep this `AGENTS.md` synchronized with those files.

## Code Style Guidelines (C#)

### Namespace and Layout

- Use file-scoped namespaces (`namespace X.Y;`).
- Keep one primary type per file unless partial split is intentional (for example `*.Log.cs`).
- Keep folder names aligned with namespace segments.

### Imports

- Keep explicit and minimal `using` directives.
- Remove unused imports.
- `using static` is acceptable when it improves readability.

### Formatting

- Use 4 spaces indentation for `*.cs` files.
- Keep blocks compact when readable; single-line blocks are allowed.
- Prefer explicit object creation types.
- Follow modifier ordering from `.editorconfig`.

### Types and Nullability

- Prefer explicit types over `var` (`csharp_style_var_* = false`).
- Keep nullable-reference warnings clean; avoid suppressions.
- Use generic constraints like `where T : notnull` where appropriate.
- Use nullability flow annotations such as `[NotNullWhen(true)]` for out parameters.

### Naming

- Public API: PascalCase.
- Private fields: `_camelCase`.
- Generic type names: `TKey`, `TState`, `TSettings` style.
- Use `Try*` naming for non-throwing creation/parse patterns.

### API and Docs

- Preserve XML docs for public contracts.
- Keep `[PublicAPI]` attributes consistent with surrounding code.
- Public API analyzers are enabled; avoid accidental API surface drift.

### Error Handling and Validation

- Validate invariants early (for example via `Validator.RequireThat`).
- Check ranges/arguments before mutating internal state.
- Prefer fail-fast behavior for invalid conditions.
- Do not swallow exceptions unless there is explicit recovery logic.

### Logging

- Use source-generated logging (`[LoggerMessage]`) for structured logs.
- Keep message templates parameterized.
- Reserve verbose details for `Trace`; use `Debug` for lifecycle milestones.

### Performance Patterns

- Favor spans and low-allocation patterns on hot paths.
- Hoist repeated allocations out of loops when possible.
- Keep algorithmic data structures (`struct`, arrays) consistent with existing patterns.

### Testing

- Add/adjust tests under `Src/FastMPH.Tests`.
- Keep tests deterministic in assertions.
- For packing/unpacking paths, validate both serialization and functional behavior.

## Pre-PR Checklist

- `dotnet build -c Release FastMPH.slnx`
- `dotnet test Src/FastMPH.Tests/FastMPH.Tests.csproj`
- If lint requested: `dotnet build FastMPH.slnx -p:RunAnalyzersDuringBuild=true`
- If packaging touched: `dotnet pack -c Release -o tmp FastMPH.slnx`
