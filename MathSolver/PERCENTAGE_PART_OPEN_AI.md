# Percentage word problems: a subset of a known whole

The **Varied wording / Diễn đạt đa dạng** switch supports only
`PercentageQuizType.FindPercentageValue` through `percentage-part-open/` profiles.
The explicit relation ID is `known-whole-find-percentage-part`. The original
percentage algorithms and closed profiles retain their existing ranges and
multi-step behavior. Ratio, inverse, remaining-amount and financial-change
relations are not part of this open-prose pilot. Remaining amounts need their
own relation, rather than treating the supplied percentage as its complement.

Typed `PercentagePartOpen.Contexts` lists supply the owner, counted whole unit,
whole noun, subset noun, answer unit, capacity, relation ID and reference clauses.
Initial contexts are storybooks within books, squared notebooks within notebooks,
and orange trees within trees, in Vietnamese and English. Examples are references,
not an exhaustive set of permitted sentences.

C# selects 10%, 20%, 25%, 50% or 75%, then chooses an integral whole whose subset
count is integral. Whole counts start at 20 and range up to the context capacity,
bounded by the selected star tier. Every tier retains the same single relation.
The whole is the 100% base; the subset percentage does not describe growth, use,
discount or the remaining stock. C# owns the calculation, answer, solution and
fresh practice/PDF facts.

| Binding | Meaning |
|---|---|
| `{f0}` | Count of the whole at 100% |
| `{f1}` | Percentage of that same whole, **including `%` supplied by C#** |
| `{v0}` | Owner of the whole and subset |
| `{v1}` | Counted unit of the whole |
| `{v2}` | Whole object noun |
| `{v3}` | Subset object noun |
| `{v4}` | Counted answer unit for the subset |

GBNF controls JSON fields and required bindings without enumerating sentences.
The model must not add a percent sign, fill numbers/names, supply answers or add
events. Syntax validation checks exact field-specific bindings; meaning checks
prove complete clauses for ownership, the original base, the subset relationship
and the requested count. A keyword or a correct C# numeric answer alone is not
sufficient. Clear changes, complements, inverted bases and percentage/whole
targets are rejected; unfamiliar clauses need review even if they may be valid.

Verified drafts follow the existing auto-save/manual-save policy. Complete but
unproved drafts are quarantined after one generation; the batch moves on without
trying to rewrite them. Invalid or duplicate output retains the existing retry
limit. Pending drafts enter neither practice nor PDF until explicit approval.
SQLite and Excel preserve the profile, schema, raw prose and review provenance.

The existing percentage exercise selection and PDF factory consume approved
profiles. Fresh numbers retain the same owner/subset schema; printing freezes
the preview facts and never invokes the model.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --percentage-part-open
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --percentage-part-open-model <model.gguf> <new-directory>
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence <directory>
dotnet run --project UnitTest/QuizPdfTests -c Release -- --percentage-part-open <output-root>
```

Real inference evidence belongs in `artifacts/verification/percentage-part-open/`.
Read each output independently of C# disposition: correct/clear, correct/awkward,
ambiguous, wrong relation and malformed prose. Track reference copies, repeated
patterns across contexts, retries and speed. Six native cases cannot establish a
general accuracy rate or certify all future wording.
