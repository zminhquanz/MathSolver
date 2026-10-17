# Open AI prose: a fraction donated, find the remainder

`ElementaryQuizType.FractionRemaining` is an independent, appended subtype. Its
relation is `known-whole-donated-find-remaining`, its persisted profiles begin
`fraction-remaining-open/`. Existing numeric enum values and donated-part/whole
profiles retain their meanings and schemas.

Select **Tìm phần còn lại sau khi cho một phần** under fractions in practice,
PDF or AI generation. Enable **Diễn đạt đa dạng** for open AI wording. The
algorithmic route is available without a model or any saved bank questions.

The bilingual typed `FractionRemainingOpen.Contexts` list supplies donor,
recipient, counted item unit, capacity, action (`donate`), relation and reference
clauses. Books, pens and notebooks are supported. A donor and recipient must be
distinct. The references are examples, not an enumeration of open sentences.

The UI label is **Diễn đạt đa dạng** / **Varied wording**. The localization key
`AiBank.Review.Experimental` remains stable for compatibility; its displayed
label no longer has an experimental suffix.

C# selects a proper fraction and a whole divisible by its denominator. Both
the donated count and remainder are positive integers. All five star tiers keep
one donation and one target; the number ranges vary without additional events.
C# computes `W - W * n / d`, solutions, choices and grading. The diagram shows
the original whole and donated fraction, without revealing the remaining count.

| Binding | Role |
|---|---|
| `{f0}` | Original whole count |
| `{f1}` | Fraction donated from that same original whole |
| `{v0}` | Donor and owner of the original whole |
| `{v1}` | Counted item unit |
| `{v2}` | Recipient of the single donation |

The whole clause contains `{f0}`, `{v0}`, `{v1}`. The transfer contains `{f1}`,
`{v0}`, `{v1}`, `{v2}`. The question contains `{v0}`, `{v1}` and asks the donor's
remaining count. The solution lead contains `{v1}`. GBNF preserves JSON and
the exact per-field slot multiset without listing full sentences. The model
does not generate numeric facts, calculations, answers or additional parties.
The gift field permits permutations of all four roles, including recipient-first
and quantity-first passive clauses; it does not lock their order to the reference.
The whole field also permits unit-first count statements. Quantity and unit
are not forced into an adjacent phrase: a clause such as "The original number
of ... was ..." still has to prove ownership and the original-count role.

Meaning verification proves complete clauses for original ownership, transfer
direction and fraction base, then the remaining-count question and lead.
Recognized reversals, complements, additional transfers, negated transfer facts or donated/
original-whole targets are rejected. Unfamiliar complete clauses go to review;
mentioning a donation in an unfamiliar valid remaining-count question alone is
not evidence of a changed target. There is no automatic approval of an unknown
clause based solely on the presence of role keywords.
An unfamiliar question or lead saying "not donated" may also mean the remainder:
it needs review, whereas a fact saying the stated donation did not happen is a
contradiction. The offline tests distinguish these cases.

Meaning proof includes explicit passive transfers and ordinary retained-count
questions such as Vietnamese "Số ... mà ... giữ lại" and English "How many ...
does ... still hold?". Each recognized clause still binds the donor, recipient,
original base, item unit and target. Trailing conditions and reversed roles are
not accepted. Known awkward wording is preserved for editorial review with its
reason, rather than repaired, retried or automatically inserted. Clear relation
contradictions are checked before the editorial gate.
Recipient-first receipt clauses must explicitly bind the source person with
"from"/"từ" and the fraction to that person's original stock. Mentioning the
person with "for" or using a fraction of one item does not prove that relation.
Unbound A/B actors and placeholder-definition notes are invalid text, so they
retain the existing retry behavior. After filling the slots, sentence openings
are capitalized; this changes display casing only, not the meaning of a draft.
The prompt uses short, language-specific role descriptions and a few positive
structure suggestions for original counts and passive gifts. These are examples
in the prompt, not whole sentences enumerated in GBNF. Reusing their structure
is not evidence of independently novel composition; native reports distinguish
that from merely differing from the canonical JSON example.

Verified drafts follow the existing auto/manual save setting. An unproved
complete draft goes to pending review after one generation, then the batch
continues. Definite invalid/duplicate output retains the existing retry limit.
Pending drafts cannot enter practice or PDF without explicit approval. SQLite
and Excel preserve profiles and review provenance. Approved templates resample
numbers within the same schema. PDF freezes preview facts and never calls AI.

Percentage remainder and multi-transfer fraction relations are not opened by this
change. The subsequent inverse donated-part-to-original-whole pilot is documented
separately in `FRACTION_WHOLE_OPEN_AI.md`.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --fraction-remaining-open
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --fraction-remaining-open-model <model.gguf> <new-directory>
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence <directory>
dotnet run --project UnitTest/QuizPdfTests -c Release -- --fraction-remaining-open <output-root>
```

Native cases cover three contexts in both languages with different C# seeds and
tiers 2–4. Read each candidate separately: correct and clear, correct but awkward,
ambiguous, wrong relationship, or malformed. Track duplicates, retries, wall
time and token speed independently of automatic proof. Six requests are a small
diagnostic sample, not a general accuracy estimate. Evidence and the report are
under `artifacts/verification/fraction-remaining-open/`.
The subsequent fresh native generation with the editorial changes and final
negation guard is recorded separately under
`artifacts/verification/fraction-remaining-editorial/`; earlier captures remain
historical evidence, not new generations after these changes.
