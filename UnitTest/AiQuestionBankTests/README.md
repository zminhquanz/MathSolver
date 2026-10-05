# AI question bank checks

This is the new optional question-bank implementation. It does not restore the
previous AI practice source, validators, benchmark or model test fixtures.

Run from the repository root:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release
```

The default run uses deterministic model responses and temporary SQLite files.
It checks all four operations, five star levels and both languages; mutations of
the generated prose; exact C# answers in each practice mode; persistence,
deduplication and matching by operation/stars/language; manual/automatic insertion;
random 50/50 source mixing with fresh C# questions even when SQLite is populated,
preserved fresh operands and all answer modes, and empty/unreadable bank fallback;
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
