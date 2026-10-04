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

Version 2 checks cover 968 combinations of mathematical structure, allowed stars,
unit family and language. They instantiate fresh C# values in every answer mode,
reject incorrect placeholders, extra numeric facts, reversed relations and wrong
solution targets, and verify template deduplication independently of preview
values. SQLite/Excel round-trips retain structure, secondary actors, unit IDs and
prose solution leads. The 100-entry Vietnamese name pools and legacy fixed-fact
questions are also checked.

Version 3 addition checks cover 870 bilingual scene/relation/unit/star combinations,
including production, harvesting, periods, garden parts, groups, arrivals and stock.
They verify fresh C# values, scene quantity limits, existing answer modes and essay
grading, accepted natural action prose, reversed comparisons, wrong period/actor/
target/event/unit rejection, topic/relationship rotation, skewed SQLite row counts,
persistent selection after restart, deduplication, Excel metadata round-trips,
forged indexed metadata rejection and migration from a pre-addition SQLite schema.

Run only these checks with `--addition-tests`. A real-model addition check covers
five mathematical relationships, different scene roles, stars 1–5 and both languages.
It includes independent family holdings at 1 and 5 stars to reproduce the
reported `given_a` / `given_b` / `question` generation failure:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests/AiQuestionBankTests.csproj -c Release -- --addition-model "E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf"
```

This checks native grammar, streamed JSON, production validation and fresh C# essay
grading with at most three attempts per case. It runs no benchmark and does not
write the app's database.

Use `--addition-model-stock <GGUF path>` for only those two family-holdings cases.
The default checks also cover repeated explanations, worked answers appended to
the question, duplicated actors/units, and short valid paraphrases.
Joined-clause checks cover comma-separated facts in live and completed previews,
lowercase ordinary/role openings, preserved proper names, and unchanged raw
historical JSON used for hashing.

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
