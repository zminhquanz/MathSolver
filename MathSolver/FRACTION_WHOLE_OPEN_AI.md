# Open AI prose: given a donated part, find the original whole

The existing `WholeFromFraction` subtype now has a separate open-prose pilot,
`fraction-whole-open/{books,pens,notebooks}`. It describes one gift only. Other
whole-from-fraction contexts, including their existing multi-step algorithms,
keep their current behavior. Enable **Diễn đạt đa dạng / Varied wording** when
adding AI questions to select the new contexts.

`FractionWholeOpen.Contexts` in both quiz JSON packs is loaded as a typed C# list.
Each context binds donor, recipient, counted item, action, original capacity,
known/base/target roles and reference sentences. C# chooses an original whole
divisible by the denominator, then computes the donated integer count. Only the
donated count and the corresponding fraction become numeric givens. For example,
8 books donated as 1/3 of the original count gives `8 ÷ 1 × 3 = 24` books.

| Slot | Role |
| --- | --- |
| `{f0}` | Known donated count, never the whole or remainder |
| `{f1}` | Donated fraction of the giver's original stock |
| `{v0}` | Giver |
| `{v1}` | Counted item unit |
| `{v2}` | Recipient |

The first fact binds all four gift roles. The second binds the donated items,
fraction and original stock of the same giver. The question asks for that giver's
original count; the lead names the original count. JSON grammar preserves each
field's required slot multiset while permitting reordered clauses and passive
gifts, without enumerating every possible full sentence. Prompts invite new
wording while keeping the single event and roles.

Meaning validation proves complete clauses, including source-bound
recipient-first receipt. Recognized giver/recipient reversal, a known original
or retained count, fraction of the retained stock, extra transfer or donated/
remaining-count question is invalid and follows the existing retry policy.
Complete but unproved wording goes immediately to review and the batch moves on.
Unknown clauses are not approved solely because all placeholders are present.
Known placeholder explanations are invalid text; unfamiliar awkward prose still
needs editorial review. No claim is made that the verifier understands arbitrary
natural language or catches every writing defect.

Verified templates follow the save setting, persist in SQLite/Excel and resample
within their meaning schema. Pending drafts cannot enter practice or PDFs before
explicit approval. C# retains all answers and inverse calculations. The preview
and worksheet show the donated count and fraction with **original whole = ?**;
printing freezes those facts and never calls AI. Direct C# callers can request a
pilot profile through `GenerateFractionQuantityStory`.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --fraction-whole-open
dotnet run --project UnitTest/QuizPdfTests -c Release -- --fraction-whole-open artifacts/verification/fraction-whole-open/pdf
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --fraction-whole-open-model <model.gguf> <new-directory>
dotnet run --no-build --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence <directory>
```

Fresh native evidence, independent reading and final-source replay are kept in
`artifacts/verification/fraction-whole-open/REPORT.md`. Read linguistic quality
separately from relation proof; six bilingual requests are a diagnostic sample.
