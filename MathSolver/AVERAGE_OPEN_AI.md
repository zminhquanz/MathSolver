# Arithmetic mean: complete independent group observations

The experimental **Diễn đạt đa dạng / Varied wording** switch now supports
`AverageQuizType.Direct`. Only this new open-prose relation is enabled. Existing
direct, missing-value, two-group weighted and other average profiles retain
their existing generator and validator routes.

Profiles prefixed `average-open/` use three distinct independent groups. Typed
`AverageOpen.Contexts` lists in the Vietnamese and English content packs provide
group labels, counted/measurement unit, action verb forms, capacity and reference
clauses. Initial activities are notebook collection, paper recycling and tree
planting. References do not enumerate the permitted sentences.

C# generates all three positive observations, the group count, the exact integer
mean, choices and the worked calculation. The pilot stays at three groups for
all five star levels; stars vary numeric ranges. The AI owns only prose. Its
bindings have these roles:

| Binding | Role |
| --- | --- |
| `{f0}`, `{f1}`, `{f2}` | Individual observations, never averages or member counts |
| `{f3}` | Complete independent group count |
| `{v0}`, `{v1}`, `{v2}` | Distinct group labels in the same observation order |
| `{v3}` | Common unit of the observed amount |
| `{v4}`, `{v7}` | Singular and plural group nouns |
| `{v5}`, `{v6}` | Completed and base forms of the same action |

Verb placeholders keep the selected action immutable while the model changes
sentence structure and connective wording. They are not numeric units. GBNF
constrains JSON and required variables without listing complete sentences.
Owner/action and group/base-action phrases stay together in grammar. The prompt
also explains the selected verb and unit values as context, while output retains
the placeholders; this avoids treating a verb variable as a group name or adding
a technical definition inside the question.
Syntax validation checks field-specific placeholder multisets and role/step IDs,
language, numeric insertion and technical-text leaks. Meaning validation checks
complete clauses for observation ownership, action, group independence, requested
per-group mean and solution target. The question itself must explicitly ask for
the mean; an average label in the solution cannot compensate for an individual
group question. Unknown wording needs review. Explicit
overlap, observations changed to means, totals or per-person/weighted targets
are rejected; such output can use the existing retry limit.

Unknown meaning is quarantined after one generation and the batch continues.
Verified drafts follow the existing auto-save/manual-save policy. Pending drafts
cannot enter practice or PDF until explicitly approved. SQLite/Excel preserve
the profile and review provenance. Fresh facts retain context/schema and the
complete-group relation; worksheets freeze preview data and never invoke AI.

Tests:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --average-open
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --average-open-model <model.gguf> <new-evidence-directory>
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence <evidence-directory>
dotnet run --project UnitTest/QuizPdfTests -c Release -- --artifacts <output-root>
```

See `artifacts/verification/average-open/REPORT.md` for real model outputs and
per-output reading. Report correct-but-awkward, ambiguous and wrong-relation
outputs separately (also record clear correct prose or malformed output when
present). Compare these judgments to automated disposition, retries, exact
duplicate templates within a context, repeated wording across contexts and
speed. Manual reading is an assessment of this sample, not another automatic
validator or permission to insert pending drafts. Do not equate all review-only
outputs with mathematically wrong questions.

The final pilot verified one mathematical relation whose English question still
had a tense/grammar defect. This verifier is not a complete editorial quality
check; distinguish mathematical acceptance from readiness for pupils. The report
also preserves the earlier false positive (missing mean in the question), its
regression fix and a separate audit of old evidence. Unchanged replay now checks
generation dispositions including duplicates; `--open-prose-audit` deliberately
allows comparison against changed source and must not be presented as a fresh run.
