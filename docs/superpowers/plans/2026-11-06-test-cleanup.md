# Unit Test Cleanup Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan in the current session.

**Goal:** Reduce duplicated test scaffolding and routine test cost while preserving independent regression coverage.

**Architecture:** Keep the three existing console suites and their production dependencies. Reuse TestSupport assertions and identical source-selection fixtures; replace PDF CLI branches with one command registry. Keep performance measurements available through explicit group filters.

**Tech Stack:** C# / .NET 10 console test harnesses.

**Spec:** The user's request to analyze and implement cleanup of redundant Unit Test code.

## Global Constraints

- Change test code and its documentation only; retain all distinct regression cases.
- Preserve existing PDF command names, output ownership, exit codes and calendar variants.
- Keep full-range integer, cancellation, storage, language and grading checks.
- Preserve pre-existing untracked device/UI harnesses and documents.
- Work on chore/test-cleanup-20261106; do not push this task automatically.

## Review Focus

- Assertion failures must not be caught as expected production InvalidOperationException.
- Similar Random fixtures with different behavior must remain separate.
- PDF percentage-part selection must still run both part and remaining checks.
- Calendar inclusive/adjacent/February/leap flags must retain their mapping.
- Correctness groups remain in the default suite; only timing benchmarks move out.

## Tasks

- [x] Record the current runner behavior and identify exact duplicate helpers.
- [x] Remove redundant Check wrappers; share only identical saved-bank Random fixtures.
- [x] Remove the self-comparison assertion in the responsive layout sweep; keep boundary and invariant assertions.
- [x] Move uniform-scan and parallel timing benchmarks to the existing explicit benchmark list.
- [x] Replace PDF CLI booleans/branches with a command table; run real command fixtures and usage-error checks.
- [x] Run all three offline suites, compare group inventory, review the diff and report remaining limitations.

## Execution Notes

Baseline: main cab436a was verified immediately before this task with three passing offline suites. No tracked modifications were present. Analysis found that apparently unused stub/partial methods are referenced by linked production files, so they are retained. Full benchmark removal and wholesale seed reduction were rejected because they would lose useful performance evidence or case diversity.

Helper audit: 73 forwarding assertions, 5 legacy exception-based assertions and 29 identical Random fixtures were consolidated across 83 files. A source comparison verified that those files contain only the intended helper removals/replacements. All 168 registered math groups remain (161 default, 7 explicit measurements). A fresh reviewer found no blocking coverage or CLI-routing issue. The initial PDF build retried copies while baseline CLI checks were running; subsequent validation runs are serialized by project.

Final verification: MathPuzzleUnitTests 161/161; AiQuestionBankTests full offline suite passed; QuizPdfTests full offline suite passed. All 26 focused PDF routes and four usage-error paths passed. The eight recorded calendar/percentage/rectangle baseline summaries match the refactored command results. Final suite logs contain no test failures or compiler warnings/errors. Evidence: artifacts/test-cleanup-20261106/. No model inference, device UI execution or opt-in performance sweep was needed for these test-only changes.
