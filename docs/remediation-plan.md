# Review remediation plan

Implementation is on a dedicated coordination worktree. GitHub issues remain open until integration verification.

## P1 — complete before P2

- [x] #4 JSON round-trip history and state
- [x] #5 Streaming history and tool follow-up context
- [x] #6 Tool-turn chronology and cache consistency
- [x] #7 Compression chronology
- [x] #8 Strict estimated budget including tool payloads

## P2

- [x] #9 Context option semantics and validation
- [x] #10 Bounded tool rounds, cancellation and execution parity
- [x] #11 Declared schema validation
- [x] #12 Streaming fragments, empty chunks and errors
- [x] #13 Accurate documentation, development instructions and behavioral tests
- [x] #14 Remove or implement nonexistent benchmarks
- [x] #15 Correct package metadata and verify package contents

## 2026-09-07 — P1 completion

Implemented #4–#8 with persistence, request-history and budget regressions. All 99 .NET tests pass. Preserved the reviewed request/stream API migration and usage examples on the .NET 10 integration baseline. P2 work follows; issues await integration verification.


## 2026-09-07 — P2 completion

Implemented #9–#15 after P1 commit 8e97985. All 166 .NET tests pass. Library line coverage is 95.5%; combined library/example coverage is 87.2% lines and 84.3% branches. Context options now have explicit semantics; both orchestration modes share bounded rounds, cancellation, validation and error handling; streaming assembles interleaved tool fragments and rejects provider errors.

Replaced stale documentation, compiled and ran README snippets, and ran both usage and legacy demos. Both hook setup scripts succeed. Removed the nonexistent benchmark workflow and verified package repository URL, README, icon and Apache-2.0 metadata against the unchanged root LICENSE. Example projects are non-packable.

Validation:
- dotnet format --no-restore
- dotnet build --no-restore
- dotnet test --no-restore --collect:"XPlat Code Coverage" --results-directory ./TestResults/Verified
- ReportGenerator HTML/TextSummary under TestResults/Verified/CoverageReport
- dotnet run --project examples/Andy.Context.UsageExamples/Andy.Context.UsageExamples.csproj (also with -- --demo)
- README snippet smoke application under ignored artifacts/ReadmeSmoke
- dotnet pack src/Andy.Context/Andy.Context.csproj --configuration Release --output ./artifacts, followed by ZIP/XML metadata inspection
- Shell and PowerShell hook setup scripts
- git diff --check

These are local implementation milestones, not integration closure. No branch was pushed or merged and no issue was closed. Integration must verify the committed branch. Provider interoperability, full JSON Schema, semantic compression, automatic retries and benchmarking remain explicit limitations rather than claimed features.
