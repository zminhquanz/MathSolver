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
The grammar binds owner/action phrases with active and English passive alternatives,
keeping the selected action inside its group observation instead of a dangling
sentence. In these single-observation fields the amount and unit can also
move independently, permitting unit-first clauses such as 'The number of ...'.
In the question, the group/base-action phrase stays together so the verb cannot
become a count noun or a dangling placeholder after generated punctuation.
Field-specific placeholder checks and meaning validation preserve their roles.
The average-specific prompt
explains the selected verb and unit values as context, while output retains the
placeholders. It teaches Vietnamese question word order and English `did` + base
verb, and asks the model to avoid repeating an action already inside a binding.
The examples illustrate grammatical constructions, not a closed list of sentences.
Syntax validation checks field-specific placeholder multisets and role/step IDs,
language, numeric insertion and technical-text leaks. Meaning validation checks
complete clauses for observation ownership, action, group independence, requested
per-group mean and solution target. The question itself must explicitly ask for
the mean; an average label in the solution cannot compensate for an individual
group question. A separate editorial guard sends known awkward wording to review:
Vietnamese units before `bao nhiêu`, repeated selected actions or `được`, detached
action placeholders, repeated average markers, and English base verbs following
`each group` without `did`. Automatic acceptance also
requires a recognized complete, well-formed question. No automatic prose rewrite
or extra model call repairs an awkward draft. Review reasons distinguish these
editorial concerns from unproved relationships while retaining the existing
`SemanticReviewRequired` state and one-attempt review policy. Unknown wording
needs review. Explicit
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

The earlier pilot verified one mathematical relation whose English question still
had a tense/grammar defect. That question now needs editorial review; a direct
individual-group question previously left unproved is now recognized as a wrong
target. This remains a bounded verifier, not a complete grammar checker or a
guarantee of readiness for pupils. The original report preserves the earlier
false positive (missing mean in the question), its regression fix and old evidence.
See `artifacts/verification/average-editorial/REPORT.md` for the new native run,
per-output reading and separate audit of the previous six outputs.
Unchanged replay checks
generation dispositions including duplicates; `--open-prose-audit` deliberately
allows comparison against changed source and must not be presented as a fresh run.
