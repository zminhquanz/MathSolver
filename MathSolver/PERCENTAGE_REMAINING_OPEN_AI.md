# Percentage of an original stock donated: find the remainder

`PercentageQuizType.FindRemainingAfterDonation` is appended to the existing enum,
preserving the first three persisted values. It has a separate relation,
`known-original-whole-donated-percentage-find-remaining`, and profiles
`percentage-remaining-open/{books,pens,notebooks}`. It is not the static subset
relation of `FindPercentageValue`.

Both quiz JSON packs supply typed `PercentageRemainingOpen.Contexts`: giver,
recipient, item unit, donation action, original-whole/rate/target roles, capacity
and reference clauses. Rates are 10, 20, 25, 50 and 75 percent. C# picks a whole
divisible by the rate's quantum, computes donated and retained counts exactly,
and never introduces a second gift, receipt or discount. Higher stars vary the
number range; this initial relation stays one gift at every star level.

For an original 80 books and 25% donated, the answer is
`80 − 80 × 25 ÷ 100 = 60`. The supplied rate always names the donated portion of
the original stock, never the remaining rate or a rate of an intermediate stock.

| Slot | Meaning |
| --- | --- |
| `{f0}` | Original whole, the 100% base |
| `{f1}` | Donated percentage of that same whole, including `%` |
| `{v0}` | Giver |
| `{v1}` | Counted item unit |
| `{v2}` | Recipient |

The first fact states original ownership, the second the directed gift of a
percentage, and the question/lead name the giver's remaining count. Grammar
retains the exact per-field slot multiset and permits reordered roles and
unit-first original-count statements. It does not enumerate every full sentence.
AI may vary wording; C# owns quantities, calculations, solutions and answers.

The complete-clause gift/base/remaining-target proofs are shared with the
fraction-remainder verifier. Percentage contracts and arithmetic are separate.
Known reversals, complemented rates, retained-stock bases, extra transfers and
donated/original-count targets are invalid. Unresolved owners and unfamiliar
complete wording go to review once, then the batch continues. A definite wrong
target takes precedence over an ambiguous pronoun. Pending templates cannot
enter practice/PDF without explicit approval.

The subtype is selectable in AI authoring, practice and the independent PDF
settings. Direct C# generation is available through the normal percentage
generator. Existing percentage subtypes keep their behavior; this subtype does
not combine quantities at high stars and does not alter the core mixed-mode
curriculum pool. It is registered as a grade-5 extension. SQLite/Excel retain
the separate profile. PDF freezes preview facts and writes separate worksheet
and answer files without calling AI.

This change also improves fraction whole-from-part proof for the previously
unproved, clear VI/EN structures. It preserves review for unresolved stock
owners and poor counted-object wording. A fresh model run after those changes
precedes the new percentage model run; final reports distinguish generation
from replay.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --fraction-whole-open
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --percentage-remaining-open
dotnet run --project UnitTest/QuizPdfTests -c Release -- --percentage-remaining-open artifacts/verification/percentage-remaining-open/pdf
dotnet run --no-build --project UnitTest/AiQuestionBankTests -c Release -- --fraction-whole-open-model <model.gguf> <new-whole-directory>
dotnet run --no-build --project UnitTest/AiQuestionBankTests -c Release -- --percentage-remaining-open-model <model.gguf> <new-percentage-directory>
dotnet run --no-build --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence <directory>
```

Evidence: `artifacts/verification/percentage-remaining-open/REPORT.md`. Read every
draft for linguistic quality and original-base clarity independently of automatic
proof. Small native samples do not establish general correctness or diversity.

The v2 follow-up keeps this same relation and scope. Prompt instructions distinguish
the two people from the counted item noun. Grammar permits freely generated words
and reordered slots but only one sentence per field; it does not list full
sentences. The verifier rejects using the item unit as a person, recipient-owned
percentage bases and retained-percentage substitutions. Extra complete-clause
proofs cover original-count adverb positions and donor-remainder questions without
rewriting the model's text. Deterministic guards also reject adding a second
percent unit after the already-formatted rate, or a spelled-out literal item count
beside the numeric placeholder. Cumbersome count wording and unresolved references
still require review. See `artifacts/verification/percentage-remaining-open-v2/REPORT.md`
for a fresh native run against the follow-up code, separately from the v1 evidence.

The English v4 follow-up proves complete original-stock clauses ending in
`at the start`, `initially`, or `originally`, and complete post-gift ownership
questions such as `How many ... does ... possess now?`. All other stock, transfer,
base and lead roles must still be proved. These are comparison normalizations;
stored wording is preserved. Noun-phrase questions remain editorial reviews.
The v5 completion keeps unpunctuated facts/questions in editorial review. Its
English-only grammar requires periods on facts and a question mark on the question,
preserving free wording and slots. This prevents run-on assembled problems without
silently adding punctuation or retrying an editorial issue as wrong mathematics.
Function notation and invented letter definitions are invalid in this count-only
pilot and follow the existing invalid-output retry policy. Ordinary parenthesized
prose is not treated as a function. No other open-prose family was expanded.
The final v7 English guard also rejects single-letter aliases fused to supplied
slots (including an intervening apostrophe) and ownership attributed to an unbound single-letter possessive, while
preserving possessives of the supplied owner. Both `starts with` and `started with`
bind the original stock. Focused follow-up commands are
`--percentage-remaining-english` and `--percentage-remaining-english-model` in the
AI test runner; the latter generates the three English contexts only.
Evidence: `artifacts/verification/percentage-remaining-english/REPORT.md`.
