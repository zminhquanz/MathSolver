<!--
Sync Impact Report
Version change: unversioned scaffold -> 1.0.0 (initial adoption)
Modified principles: no previously adopted principles; the five template slots now define:
- I. Mathematical Correctness and Explainable Results
- II. Offline Core and User Control
- III. Deterministic Mathematics, Optional AI
- IV. Responsive and Localized Learning Experience
- V. Verified Performance and Resource Safety
Added sections: populated Core Principles, Architecture and Compatibility,
Development Workflow and Quality Gates, and Governance from the resolved scaffold.
Removed sections: none.
Deferred placeholders and follow-up TODOs: none.
Dependent templates and commands: unchanged; read this constitution at runtime.
Review note: remove this temporary report before committing the constitution.
-->
# Math Solver Constitution

## Core Principles

### I. Mathematical Correctness and Explainable Results

Calculators, question generators, grading, and exports MUST preserve the mathematical value
of accepted input. Each engine MUST define its supported domain, precision, rounding, and
undefined cases; unsupported input MUST produce an explicit error rather than silent
truncation, overflow, or a fabricated result. Integer and rational operations MUST use exact
arithmetic where their contracts require it. Approximate results MUST follow a documented
precision and rounding policy. Display shortening MUST NOT change the underlying value or
the full-value copy/export contract.

Learning tools MUST provide calculation steps or a mathematical explanation consistent with
the answer. Shared formulas and mathematical facts MUST come from common engines or catalogs
so solving, practice, diagrams, and reference material agree. Correctness tests MUST compare
against independently derived expectations, identities, or reference arithmetic.

### II. Offline Core and User Control

Calculators, C#-generated practice, formula references, and multiplication tables MUST remain
usable without a network connection, account, subscription, advertisement, donation, or AI
model. Optional downloads and external links MUST be clearly initiated by the user; failure
of those services MUST NOT disable core mathematics. Model inference MUST remain local
unless a future constitution amendment explicitly changes that policy.

Edits, navigation, and recoverable failures MUST preserve accepted input and completed work
unless the user explicitly clears or replaces it. Imports MUST validate into temporary state
before replacing existing data, and database writes MUST have transaction boundaries that
prevent partial changes on failure or cancellation. Secrets and private signing keys MUST
remain outside the repository. These rules protect learning access and user-owned work.

### III. Deterministic Mathematics, Optional AI

C# engines MUST own numeric facts, mathematical relationships, units, difficulty rules,
answers, and grading. AI MAY supply wording within supported, typed question contracts; it
MUST NOT become the authority for mathematical truth or silently broaden those contracts.
Model output and imported questions MUST pass structural, mathematical, and supported prose
validation before storage or use. Validation limits MUST be documented rather than presented
as a general proof of natural-language correctness.

Practice MUST remain independent of live inference and MUST fall back to the C# generator
when a suitable validated bank entry is unavailable. Existing readable question formats MUST
remain supported or receive an explicit migration plan. AI prompts, retries, batches, and
contexts MUST have enforced bounds, and cancellation MUST release runtime resources while
preserving already committed valid questions.

### IV. Responsive and Localized Learning Experience

User workflows MUST remain operable at supported phone and desktop sizes, including narrow
and low-height windows. Changed layouts MUST be checked for clipped text, unreachable actions,
keyboard obstruction, scrolling, and preservation of input across navigation. Long results
MUST use bounded previews or scrolling without freezing the interface. Long-running work MUST
expose progress or busy state and a usable cancellation path where the operation supports it.

New or changed user-facing text MUST use the localization system and cover the bundled
Vietnamese and English languages. Translations MUST preserve keys, placeholders, mathematical
symbols, and numeric meaning; missing translations MUST follow the documented Vietnamese
fallback. Changed visual workflows MUST be reviewed in light and dark themes and with long
localized strings or enlarged text. Reuse shared styles, controls, formulas, and diagrams to
keep the learning experience consistent.

### V. Verified Performance and Resource Safety

Optimizations MUST preserve the numerical contract and a correct fallback. Hardware dispatch
MUST check runtime capability and applicable user settings before executing an accelerated
path. Reports MUST distinguish detected capability, selected policy, and the backend actually
executed. Unsupported hardware paths MUST be reported as untested or skipped rather than
counted as verified.

Performance claims MUST include reproducible workload, build configuration, hardware/runtime,
correctness checks, and measured results. Large computations and model jobs MUST enforce
resource admission limits, bound temporary allocations, and release buffers, workers, file
handles, contexts, and native weights on success, failure, and cancellation. Heavy work MUST
run outside the UI thread. Benchmarks MUST NOT justify weakened correctness, hidden memory
costs, or loss of interface responsiveness.

## Architecture and Compatibility

- The application uses C#, .NET MAUI, and XAML. Numerics and calculation engines MUST remain
  testable without launching MAUI; views MUST delegate shared mathematical work to engines.
  Platform-specific behavior MUST stay behind adapters or explicit platform boundaries.
- Android and Windows are the documented release platforms. Changes affecting shared code
  MUST account for both; iOS and Mac Catalyst targets MUST NOT be described as release-verified
  without corresponding evidence. Supported SDKs, OS versions, architectures, and native
  dependency requirements MUST be recorded in the project documentation and build settings.
- User-data formats, localization keys, and shared formula/question contracts MUST be treated
  as compatibility boundaries. Breaking changes MUST document migration, fallback, and the
  effect on existing saved data before adoption.
- Contributions and distributions MUST preserve the source-license notices and respect the
  separate branding terms documented in `README.md` and `LICENSE`. New dependencies and
  bundled assets MUST have their source, purpose, and license recorded.
- Repository documentation is implementation guidance, not an exemption from these
  principles. Stale platform or performance notes MUST be reconciled when the affected
  behavior changes; historical experiments MUST remain identifiable as historical.

## Development Workflow and Quality Gates

1. Feature specifications MUST state observable behavior, accepted input, numeric guarantees,
   offline behavior, platform scope, and relevant failure/cancellation cases. Plans and tasks
   MUST identify applicable constitutional rules and explain any proposed exception.
2. Changes MUST reuse existing engines and contracts where applicable. A new abstraction,
   dependency, native backend, or persistent format MUST have a concrete requirement and
   documented compatibility and resource implications.
3. Algorithm, grading, validation, storage, and lifecycle changes MUST include focused
   regression coverage. Mathematical expectations MUST remain independent of the algorithm
   under test. Storage tests MUST use isolated data; real-model and heavyweight device tests
   MUST be explicitly selected and kept separate from deterministic offline suites.
4. Before completion, contributors MUST run the relevant checks documented in
   `UnitTest/README.md` and affected suite documentation, plus builds for affected release
   targets when the environment supports them. UI changes MUST include native visual or
   interaction verification appropriate to the platform. Console tests and Windows layout
   probes MUST NOT be presented as proof of physical Android touch, IME, or rendering behavior.
5. Reviews MUST record the change, rationale, commands and outcomes, platform/backend coverage,
   and unresolved limitations. Missing SDKs, devices, models, or ISA support MUST be stated
   explicitly; a skipped check MUST NOT be reported as passed. Relevant README and architecture
   guidance MUST be updated when public behavior or development instructions change.

## Governance

This constitution governs project specifications, plans, implementation, and review. Changes
MUST be reviewed for compliance with every applicable principle. A proposal that conflicts
with a principle MUST resolve the conflict through an amendment or an explicitly approved,
bounded exception before the conflicting implementation is accepted.

Amendments MUST identify the affected rules, rationale, compatibility impact, migration or
transition work, and verification expectations. The project maintainer MUST approve amendments
before they are merged. Exceptions MUST record the approving maintainer, scope, rationale,
expiry or removal condition, and follow-up work; an exception MUST NOT silently redefine a
principle. Reviewers MUST check unresolved exceptions during subsequent affected changes.

Constitution versions use semantic versioning independently of application releases:
MAJOR for incompatible principle removal or redefinition; MINOR for a new principle, section,
or materially expanded obligation; PATCH for wording or clarifications without changed
obligations. The original ratification date MUST remain fixed, and the last-amended date MUST
change only when the constitution changes. This is the first populated constitution, adopted
on 2026-11-04; the preceding file was an unversioned scaffold.

`README.md`, `MathSolver/ARCHITECTURE.md`, `TRANSLATING.md`, and `UnitTest/README.md` supply
runtime development guidance. Dependent Spec Kit templates and commands read this constitution
at runtime; constitution amendments MUST NOT rewrite those sources as part of this workflow.
Temporary Sync Impact Reports MUST be removed before the amended constitution is committed.

**Version**: 1.0.0 | **Ratified**: 2026-11-04 | **Last Amended**: 2026-11-04
