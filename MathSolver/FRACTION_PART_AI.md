# Open prose: known whole → donated part

The experimental **Diễn đạt đa dạng / Diverse wording** switch is available for
`FractionOfNumber` in the fraction quantity family. It selects profiles with the
`fraction-part-open/` prefix; existing stored profiles and normal algorithmic
questions retain their original difficulty progression.

The initial relation is `known-whole-find-donated-part`. Its three contexts are
books, pens and notebooks, loaded as typed lists from `FractionPartOpen.Contexts`
in both language packs. The list supplies the owner, counted unit, capacity,
donation action and reference clauses. These references are examples, not an
enumeration of all allowed AI sentences.

The pilot stays one step at all five star levels. Stars vary number ranges and
proper fractions, without introducing an extra event. C# generates a positive
whole divisible by the denominator and computes the integer donated count.
It supplies the answer, worked calculation and worksheet data. The AI supplies
only wording using these immutable bindings:

| Binding | Meaning |
| --- | --- |
| `{f0}` | Original whole item count |
| `{f1}` | Proper fraction of that same whole being donated |
| `{v0}` | Owner/donor |
| `{v1}` | Counted item unit |

GBNF preserves the JSON structure and required placeholders. Syntax validation
also checks each field's placeholder multiset, role and step IDs, language,
technical-text leaks and inserted quantities. The fraction meaning verifier
then proves whole ownership, fraction base, donation and the requested donated
count with complete clause patterns. It does not infer correctness from keywords
alone. Recognized changes to remaining quantities or receipt direction are
rejected. Unproved wording goes to **Questions awaiting review** after one call,
and the batch continues. Only invalid or duplicate outputs use the existing
bounded retries. Pending drafts cannot enter practice or worksheets without
explicit review approval.

Finding the remaining quantity, finding the whole from a part, continuous
quantities and multi-step fraction stories are outside this open-prose pilot.
Their existing closed-generation routes remain available.

Verified or explicitly approved templates use the existing SQLite provider and
shared practice/PDF factory. Fresh numeric facts preserve the relation and
schema. Export freezes the preview's facts; it does not invoke AI or resample.

Verification commands:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --fraction-part-open
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --fraction-part-open-model <model.gguf> <new-evidence-directory>
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence <evidence-directory>
dotnet run --project UnitTest/QuizPdfTests -c Release -- --artifacts <output-root>
```

Real inference and synthetic fixtures must be reported separately. Evidence is
under `artifacts/verification/fraction-part-open`; the final report also links
the preceding final-source same-day-time run.
