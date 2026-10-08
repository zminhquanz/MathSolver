# AI question bank checks

`--motion-bank` checks all four motion subtypes, ten question targets, five star
levels and both languages. It verifies independent unit/direction/rest arithmetic,
reviewed alternatives, rejected unsafe prose, fresh facts in all answer modes,
SQLite/Excel and bank practice/fallback. It is also included in the default suite.

`--motion-model "C:\path\model.gguf"` runs the real app runtime with an isolated
SQLite bank, every subtype/star/language, repeated five-star schemas, all ten
targets, then a four-question auto-save job and model release. Evidence is saved
under `artifacts/verification/motion-model-*`. `--motion-budget` checks Gemma input
tokens against the runtime's context/output limits; `--motion-evidence directory`
exports rendered questions and C# solutions for human review.
See [the motion authoring guide](../../MathSolver/MOTION_AI.md).

`--structure-policy` reproduces the screenshot's invalid bread/fraction prose,
checks field-level diagnostics, safe synonyms and the removal of free prose
from the semantic grammar. It also runs in the default suite.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -- --structure-model "<GGUF path>"
dotnet run --project UnitTest/AiQuestionBankTests -- --structure-worker "<GGUF path>"
```

The first command runs 18 native cases across fractions, applied arithmetic,
Find X, two-number relations, average and percentage, in Vietnamese and English.
Each case allows up to three attempts and retains prompts, raw JSON, error
fields and token metrics under `artifacts/verification/structure-model-*`.
`--structure-repro <GGUF path>` runs only the screenshot's bread/addition case;
running it against an old build can reproduce the original grammar mismatch.
The worker command tests continuous automatic generation, SQLite saves/reads,
fresh C# facts and practice grading, streamed snapshots and release of weights
after each batch. It uses an isolated database under
`artifacts/verification/structure-worker-*`, never the installed app database.
See [verification notes](../../MathSolver/AI_STRUCTURE_VERIFICATION.md).

Quiz wording and context lists load from `MathSolver/Resources/Raw/QuizContent`.
See the [JSON authoring guide](../../MathSolver/QUIZ_CONTENT_AUTHORING.md).
`--quiz-content` checks bundled packs, typed list caching, fallback, value slots and
malformed input. `--validate-quiz-content <file>` validates an author file without
loading a model; `--check-quiz-translation <translated-file> <reference-file>` also
checks complete IDs, preserved slots and mathematical context metadata.

`--practice-formats` checks 1,080 combinations of nine groups, four operations,
five star levels, two languages and three answer modes for arithmetic and Find X.
Numeric mode preserves the original question without SQLite reads; word mode
keeps prose through both C# and bank selection, including empty/invalid-bank
fallback. Real SQLite selection preserves the two families. Also runs in the
default suite; no model inference is required.

`--fraction-bank` checks 2,448 fraction scene/star/language/seed cases covering all
nine knowledge groups, four operations and three answer modes, plus rational
grading, full-clause validation, native grammar, SQLite selection/deduplication,
Excel denominator roundtrips, background generation and instant C# fallback.
This also runs in the default suite. `--fraction-model <GGUF path>` loads a real
local model, seeds duplicate wording and generates novel validated stories for
all nine groups, in Vietnamese/English, using isolated SQLite databases under
`artifacts/verification/fraction-model-*`.

`--prose-model <GGUF path>` runs a real local model against separate verification
SQLite databases. It pre-seeds old savings givens with every supported question,
generates three distinct Vietnamese and three English stories, checks streaming,
units/roles, C# grading and insertion, verifies two Find X stories against the
same stored wording, then runs the real automatic three-item
worker against existing money questions. Logs and databases remain under
`artifacts/verification/prose-model-*`. It never writes the installed app bank.
`--prose-deduplication` also checks grammar availability after reviewed examples are exhausted, unused prompt
examples and index snapshot synchronization without loading a model.

`--reasoning-stories` checks two-number, average, percentage and multi-step templates:
640 bilingual subtype/star/seed combinations, fresh facts with an unchanged role
schema, all three answer modes, complete C# work accepted by essay grading,
swapped roles, answer leakage, wrong unit bindings, extra conditions, foreign
text, novel equivalent wording and partial JSON streaming. It also verifies
SQLite deduplication/selection, Excel interchange, background insertion and
practice selection using temporary databases. These checks run in the default
suite too.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -- --reasoning-model "<local GGUF path>"
dotnet run --project UnitTest/AiQuestionBankTests -- --reasoning-budget "<Gemma 4 GGUF path>"
```

The model command exercises three Vietnamese cases: sum/difference 3 stars,
indirect average 4 stars and whole-from-percentage 5 stars, with at most three
attempts each. It retains prompt/output/error logs under `artifacts/verification`
and ejects the model in `finally`. The budget command tokenizes 640 retry prompts
with Gemma framing and verifies the existing 2048-token context/output budget;
it runs no inference. Neither command changes the installed app database.

`--multistep-bank` additionally checks all four multi-step subtypes, five star
levels, both languages and four contexts (160 cases), every reviewed clause,
fresh facts in all answer modes, factual novelty, finite palette exhaustion,
SQLite opening-phrase deduplication and derived-index migration. It is included
in the default suite.

`--multistep-model "<local GGUF path>"` performs actual native inference: twenty
bilingual outputs including repeated generation over pre-existing SQLite prose,
followed by the real four-item auto-save worker. It validates template roles,
changed factual wording, fresh C# steps, essay grading, streaming and model
release. Separate databases, prompts and metrics are written under
`artifacts/verification/multistep-model-*`. See the
[multi-step authoring guide](../../MathSolver/MULTISTEP_AI.md) for JSON examples
and the limits of reviewed prose generation.

This is the new optional question-bank implementation. It does not restore the
previous AI practice source, validators, benchmark or model test fixtures.

Run from the repository root:

Knowledge-group profiles are covered by `LearningProfileTests`: all nine UI groups, all four operations, all stars, both languages, every eligible scene, fresh operands, reviewed variants and all answer modes. It rejects role/unit/quantity mutations and foreign text; verifies star scales, exact conversions (including l/ml), age/activity scales, area/volume dimensions, perimeter/remainder/minimum-pack answer rules, factual tables, natural solution leads, group-isolated SQLite selection, historical grade-independent selection, legacy counted-template access and merged measurement access to old mass/length/transport rows. It also checks the editable SQL grid, all-group Excel roundtrips, numeric/story C# variety, background worker insertion and cleanup. Existing v1–v4 tests remain in the full suite. These are deterministic functional checks; no GGUF inference or benchmark runs by default.

To run just the profile checks:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --learning-profiles
```

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release
```

The default run uses deterministic model responses and temporary SQLite files.
`ProseDeduplicationTests` checks question wording independently of random facts,
actor names, stars and solution leads; Unicode/case/spacing/punctuation normalization;
distinct phrasing and units; existing SQLite rows and direct SQL edit/delete index
synchronization; bilingual manual/automatic duplicate retries with rejected wording
in the prompt; the three-attempt limit; an insert-time duplicate after precheck;
pending manual-batch duplicates; and a worker rewriting a duplicate in real SQLite.
Selection tests seed historical duplicates directly, because new inserts must reject
cosmetic variants even when historical banks already contain them.

Run only the wording checks with:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -- --prose-deduplication
```
It also includes `FindXBankTests`: 11,316 bilingual scene/star/role/wording cases across
the nine knowledge groups and six unknown roles. These verify unique integer solutions
and back substitution, all three answer modes, essay units, malformed/foreign prose,
fresh facts, role-specific scene rotation, explicit AI role selection, three-attempt
failure without insertion, family-isolated SQLite selection, legacy compatibility,
Excel roundtrips and numeric/story practice mixing. They use simulated model output.

Run only the Find-X bank checks:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --findx-bank
```

It checks all four operations, five star levels and both languages; mutations of
the generated prose; exact C# answers in each practice mode; persistence,
deduplication and matching by operation/stars/language; manual/automatic insertion;
random 50/50 source mixing even when SQLite is populated, plus independent numeric/story
mixing inside C# with original random operands, answer choices and all answer modes,
and both numeric and word-problem fallbacks for an empty/unreadable bank;
the three-attempt limit; cancellation without saving partial output; failed-save
recovery; unavailable-database fallback; and cleanup after an invalid GGUF import.
Data checks cover SELECT/INSERT/UPDATE/DELETE, SQL comments, affected-row counts,
WITH and RETURNING, query bounds and cancellation/timeout, exported
SpreadsheetML schema validity, bilingual Excel round-trips, Excel shared strings
and numeric cells, duplicate detection, row errors and rollback of an import when
SQLite fails partway through. The Open XML SDK is a test-only dependency.
Invalid SQL checks assert precise SQLite codes/messages for malformed syntax,
missing tables/columns and errors during execution, without faulting the query
task or exposing partial results. They also verify that subsequent queries and
insertions remain usable.
Write checks verify rollback after partial changes from `UPDATE OR FAIL`,
cancellation and timeouts; completion of every changed row despite RETURNING
display limits; rejection of multiple statements before any write; deletion
without WHERE; and validation of directly edited data during practice/export.
It runs no model inference and no benchmark.

The grid checks also migrate an installed `Family`/`UnknownRole` schema to
`ProblemType`/`ProblemVariant`, preserving row keys, payloads, classification values
and selection indexes. They verify editing by the new names, Find-X isolation,
deduplication after migration and repeated initialization. Run these alone with:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -- --sql-grid
```

Shared one-step checks cover every reviewed C# pattern and wording variant in both languages, using the same validator as the AI bank. They verify all answer modes, correct and incorrect essay answers, fresh-number variety, smaller-amount/inverse comparisons, total-to-part relations, factor comparisons with `lần`/`times` answer units, and rejected actor/target inversions. Built-in source selection does not read SQLite. Paused AI inference cannot prevent immediate built-in practice.

Run just the shared one-step pattern checks:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --one-step-practice
```

Background-practice checks pause a simulated inference job while every answer
mode requests a question. An empty bank serves fresh C# questions immediately;
after the first auto-save, practice can read the committed SQLite question while
the next inference remains paused. Stopping the job releases the runtime and
retains saved questions. The production bank entry supports Windows and Android
and stays accessible when practice settings are collapsed. These checks do not
measure Android inference performance or prove physical-device rendering.

Language checks reproduce a saved Vietnamese question containing the Polish word
`łącznie`, including JSON-escaped letters. They cover all prose fields in versions
1–3, Vietnamese/English character boundaries, legitimate Vietnamese accents in
NFC and decomposed form, invisible/replacement characters, native grammar alphabets,
three failed retries without saving, manual insertion, existing SQLite rows, and
Excel import/export. Invalid historical rows remain available for SQL inspection
but are skipped during practice and export; no app database is modified by tests.

Memory lifecycle checks cover one weight load per batch, automatic loading on the
next job, bounded context sizing, and cleanup after successful generation, three
rejected attempts, loading/generation errors, cancellation and failed cleanup.
They also ensure that a new job cannot start while native disposal is pending.
Run the optional real-GGUF lifecycle check to generate a Vietnamese batch and an
English five-star question, validate the results, assert disposal after both jobs,
and verify automatic loading of the same selected model:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --memory-model "E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf"
```

This functional check writes only to an in-memory test store. It does not measure
RAM savings, run a performance benchmark or modify the app's SQLite database.

Version 2 checks cover 968 combinations of mathematical structure, allowed stars,
unit family and language. They instantiate fresh C# values in every answer mode,
reject incorrect placeholders, extra numeric facts, reversed relations and wrong
solution targets, and verify template deduplication independently of preview
values. SQLite/Excel round-trips retain structure, secondary actors, unit IDs and
prose solution leads. The 100-entry Vietnamese name pools and legacy fixed-fact
questions are also checked.

Version 3 addition checks cover the bilingual scene/relation/unit/star combinations,
including production, harvesting, periods, garden parts, groups, arrivals and stock.
They verify fresh C# values, scene quantity limits, existing answer modes and essay
grading, accepted natural action prose, reversed comparisons, wrong period/actor/
target/event/unit rejection, topic/relationship rotation, skewed SQLite row counts,
persistent selection after restart, deduplication, Excel metadata round-trips,
forged indexed metadata rejection and migration from a pre-addition SQLite schema.
The expanded catalogue covers 1,254 combinations across 29 settings. Checks also
require all ten requested count-context groups at every tier in both languages,
reach every eligible setting through balanced random selection, bound each prompt
to a single context/unit, and verify the native grammar cannot switch that unit.
Scale checks additionally exercise every eligible 1–5-star domain in both languages,
including minimum/maximum random draws, primary digit buckets, smaller secondary
operands, results crossing a digit boundary, total capacity, matching actor/unit/time
roles, carrying progression, and exclusion of incompatible small settings. They reject
monthly output rewritten as morning output, mixed object nouns, overlapping or
cumulative periods, and old actor patterns inconsistent with the selected scale.
Real-output regressions reject Unicode numeric facts, doubled English actor roles
and dangling conjunctions. They accept combined-period questions without the
literal word `total` and past-tense/passive removal with original-stock targets.

Run only these checks with `--addition-tests`. A real-model addition check covers
five mathematical relationships, different scene roles, stars 1–5 and both languages.
The loaded model's 75% thread budget is asserted (12 logical CPUs use 9 workers).
The full live run exercises 19 cases, including both languages at every star level,
factory production at stars 3–5, spatial nursery sections, comparisons, restored
stock and arrivals. Each accepted template is rendered with four fresh C# fact sets
and passed through the existing essay grader; the first rendering is printed for
manual review. Failed cases are collected after at most three attempts, allowing
the other selected cases to be inspected in the same run.
It includes personal holdings at 1 star and distribution stock at 5 stars to exercise
`given_a` / `given_b` / `question` at the appropriate physical scale:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --addition-model "E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf"
```

This checks native grammar, streamed JSON, production validation and fresh C# essay
grading with at most three attempts per case. It runs no benchmark and does not
write the app's database.

Use `--addition-model-stock <GGUF path>` for five stock cases: personal holdings
at 1 star and restored wholesale stock at 4 stars in both languages, plus
Vietnamese distribution stock at 5 stars.
The default checks also cover repeated explanations, worked answers appended to
the question, duplicated actors/units, and short valid paraphrases.
Joined-clause checks cover comma-separated facts in live and completed previews,
lowercase ordinary/role openings, preserved proper names, and unchanged raw
historical JSON used for hashing.
Displayed/live prose also removes repeated commas and spaces before punctuation,
while preserving raw saved JSON. A lone letter before a stock quantity, observed
in a real GGUF response, is rejected rather than saved as prose.

Rendered prose leads, equations and answer units are passed through the existing
essay grader. A lead naming an unrelated actor is rejected alongside incorrect
relations and units. Historical version 1 hashes still deduplicate on import.

Streaming checks pause a simulated model mid-response and verify visible partial
prose/JSON, incomplete string/escape/Unicode prefixes, immutable snapshots,
token metrics including empty text deltas, per-attempt speed resets, retained retry errors and separate attempt history,
complete final output, the three-attempt failure limit, cancellation and ignored
late text/metrics callbacks. Cancellation between token yields also retains the
final buffer when the executor returns normally. Incomplete JSON is never inserted into the bank.

To check a real local GGUF model explicitly:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --model "E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf"
```

This loads the selected model, generates a Vietnamese placeholder template for each operation,
checks its actual output with the production validator, and ejects the model.
It also checks that the streamed text deltas match the final JSON response.
Each accepted template is instantiated with four fresh C# value/name sets and
checked with the existing essay grader.
There are at most three attempts per operation. This is a functional check,
not a speed or memory benchmark. It never inserts questions into the app's database.

In Visual Studio, select `AiQuestionBankTests` as the startup project and press
Ctrl+F5. Like the existing math test harness, it is a console project rather than
a Test Explorer adapter.

Version 4 subtraction/multiplication/division checks cover all eligible bilingual
setting/role/unit combinations at 1–5 stars, including fresh operand buckets,
nonnegative stock, exact division, realistic groups, object/group answer units,
all answer modes and essay grading. Mutations reject swapped number/target roles,
foreign prose, negation, extra objects, forged context metadata and unbounded factors.
SQLite rotation/deduplication, Excel interchange and legacy v2 selection are included.
The English essay check also covers plural solution leads when the answer is singular.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --arithmetic-contexts
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --arithmetic-context-model "E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf"
```

The explicit model command exercises all five tiers for each of the three operations,
including Vietnamese/English, retries, live output and token metrics. It validates
each accepted template with four fresh C# fact sets and existing essay grading,
and always ejects native weights. It never writes to the app's database.

Explicit live-model performance and expanded-context checks:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --addition-throughput "E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf"
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --addition-context-model "E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf"
```

The first uses four fixed cases for before/after comparison. The second exercises
seven new settings over stars 1–5 and both languages. Each prints streamed-output
validation, rendered prose, prompt character count, allocated context, first-token
latency and decode token/s, with at most three attempts per question. Weights are
shared across the selected batch and disposed afterwards. Neither command writes
the app's question bank. Run without simultaneous builds/inference for meaningful
speed comparisons. Token/s measures decoding after the first token, excluding
prompt processing and model loading; end-to-end latency is reported separately.

## SQLite grid editing

`SqlGridTests` also verifies atomic multi-cell row updates: an invalid cell, stale
snapshot, cancellation or failing trigger must not partially save the other
cells. Projected updates preserve hidden columns, full JSON, literal NULL versus
SQL NULL and exact Int64 values. The MAUI table owns inline drafts; the detail
panel is read-only. UI compilation is checked separately on Windows/Android.

Run `dotnet run --project UnitTest/AiQuestionBankTests -- --clear-bank` for
`DeleteAllTests`: whole-bank deletion (including malformed historical records),
an empty bank, cancellation, SQLite rollback, prose-index cleanup, preview
re-saving, reopening the database and exclusion of concurrent generation/writes.
Only temporary databases are used; installed user data is never deleted.

The query results support cell updates, row insertion and deletion for direct
`BasicQuestionBank` SELECT projections that include the genuine `Hash` column.
`SELECT *` respects the actual database column order, including older migrated
databases. Aliases are mapped by column index; aggregates, expressions, joins,
CTEs and result sets without a key stay read-only.

`SqlGridTests` checks identity mapping, empty editable results, quoted text,
multiline and long JSON, SQL NULL versus literal text, exact Int64 values,
insertion, duplicate keys, deletion, optimistic conflicts, cancellation and
failed-write rollback. It also verifies that invalid manually edited JSON still
cannot enter normal practice. The tests use temporary SQLite databases, without
model inference or benchmarks. App builds compile the native editor UI; device
layout has not been verified by this harness.

## Proportion bank and real GGUF

Run --proportion-bank for all 47 templates in both languages and every permitted star tier (390 cases), fresh facts/all three modes, essay grading, hostile prose, exhausted grammar, SQLite/Excel and provider fallback. Run --proportion-model <model.gguf> for live native inference across bilingual templates and the subtype/star matrix, then the real auto-save worker. Evidence uses isolated artifacts/verification/proportion-model-* databases. --proportion-model-resume <model.gguf> <evidence-directory> revalidates saved rows before continuing a partially completed verification run.

## Decimal bank and real GGUF

`--decimal-bank` checks 320 bilingual context/subtype/star profiles, independent
exact decimal math, three answer modes, fresh values, essay grading, unsafe
prose, exhausted grammar, SQLite/Excel and provider fallback. Included in the
default full regression run.

`--decimal-model <model.gguf>` runs 40 native bilingual operation/star profiles
across eight contexts, excludes already stored wording, validates streamed JSON,
saves to isolated SQLite and regenerates/grades fresh questions. It then runs
the real auto-save worker for four questions and verifies weights are released.
Evidence is written under `artifacts/verification/decimal-model-*`.
`--decimal-model-resume <model.gguf> <evidence-directory>` revalidates prior
saved rows before continuing a partially completed run. Installed user data is
not modified. See [DECIMAL_AI.md](../../MathSolver/DECIMAL_AI.md).
