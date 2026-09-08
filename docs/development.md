# Local development

Install the .NET 10 SDK selected by global.json. Run natively with dotnet; do not use Mono or generated .exe files.

~~~bash
dotnet restore
dotnet build
dotnet test
dotnet run --project examples/Andy.Context.UsageExamples/Andy.Context.UsageExamples.csproj
dotnet run --project examples/Andy.Context.UsageExamples/Andy.Context.UsageExamples.csproj -- --demo
dotnet format --no-restore
git diff --check
~~~

New runtime behavior belongs in the xUnit assembly under tests/Andy.Context.Tests. Example helpers are a non-test library; the usage project is non-packable. Tests use recording clients and explicit streaming fragments, not live credentials.

## Coordination and hooks

Read [the coordination guide](coordination/README.md), register, and claim a worktree before editing. Install/verify the guard with:

~~~bash
./scripts/setup-git-hooks.sh
~~~

On Windows use ./scripts/setup-git-hooks.ps1. Scripts preserve an existing coordination guard and refuse to overwrite an unrelated hook. Install coord-guard with the repository's coordination tooling. Claims and single-writer locks apply to formatting and packaging.

## Coverage

~~~bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
reportgenerator '-reports:./TestResults/*/coverage.cobertura.xml' '-targetdir:./TestResults/CoverageReport' '-reporttypes:Html'
reportgenerator '-reports:./TestResults/*/coverage.cobertura.xml' '-targetdir:./TestResults/CoverageReport' '-reporttypes:TextSummary'
~~~

ReportGenerator is an optional .NET global tool, installed with dotnet tool install --global dotnet-reportgenerator-globaltool. Reports are local artifacts. Use a fresh results directory for comparisons so older runs are not merged.

## Packaging and benchmarks

~~~bash
dotnet pack src/Andy.Context/Andy.Context.csproj --configuration Release --output ./artifacts
~~~

The package includes Apache-2.0 metadata, root README, context icon and repository URL. Examples are non-packable. Inspect package contents before publishing; publication requires integration authority.

No benchmark project is maintained. The scheduled workflow targeting a nonexistent project has been removed. No performance thresholds or benchmark results are claimed.

## Tracking

[The remediation plan](remediation-plan.md) is the current checklist; no separate conversion plan exists. Update it, README status and related GitHub issues with evidence. Local implementation does not close an issue; integration verification comes first.
