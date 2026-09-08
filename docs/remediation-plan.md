# Review remediation plan

Implementation is on a dedicated coordination worktree. GitHub issues remain open until integration verification.

## P1 — complete before P2
- [x] #4 JSON round-trip history and state
- [x] #5 Streaming history and tool follow-up context
- [x] #6 Tool-turn chronology and cache consistency
- [x] #7 Compression chronology
- [x] #8 Strict estimated budget including tool payloads

## P2
- [ ] #9 Context option semantics and validation
- [ ] #10 Bounded tool rounds, cancellation and execution parity
- [ ] #11 Declared schema validation
- [ ] #12 Streaming fragments, empty chunks and errors
- [ ] #13 Accurate documentation, development instructions and behavioral tests
- [ ] #14 Remove or implement nonexistent benchmarks
- [ ] #15 Correct package metadata and verify package contents

## 2026-09-07 — P1 completion

Implemented #4–#8 with persistence, request-history and budget regressions. All 99 .NET tests pass. Preserved the reviewed request/stream API migration and usage examples on the .NET 10 integration baseline. P2 work follows; issues await integration verification.
