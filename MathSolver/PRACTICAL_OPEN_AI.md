# Practical open-prose mathematics

The diverse-prose toggle now supports these bounded profiles in addition to the existing groups:

| Family / variant | Profile prefix | Relation |
|---|---|---|
| Percentage / FindPercentageRatio | `percentage-ratio-open/` | Known whole and included subset; find their percentage ratio |
| MultiStep / MultiStepRemaining | `money-change-open/` | One purchase, unit price and quantity, cash actually tendered; find change |
| Geometry / triangle area | `triangle-area-open/` | Direct base and its corresponding perpendicular height; find area |
| Spatial / RectangleSide | `rectangle-side-open/length/` | Total perimeter and known width; find length |
| Spatial / RectangleSide | `rectangle-side-open/width/` | Total perimeter and known length; find width |

Each relation has two typed contexts in both content packs and five difficulty bounds. The creative cycle alternates rectangle length/width targets. Closed prose generation continues using the previous profiles. No enums, model packages or sampling defaults were changed.

The `*Pilot` classes own quantities, exact calculations, units and the model. The four typed `List<>` collections own context, roles and reference prose. AI can change wording and block order under `PracticalOpenGrammar`, which preserves the JSON shape, required placeholders and adjacent number/unit pairs without enumerating complete sentences. Object placeholders allow attached English possessives. Mathematical meaning is checked separately using whole-clause proofs.

Unknown wording is quarantined after its first structurally valid generation; it is not available to practice or PDF. Known wrong output or duplicate prose uses the existing retry policy. Sentence correctness and editorial quality are separate from mathematical consistency. The initial native batch is a small diagnostic sample, not an accuracy estimate for all five tiers and contexts.

Actual-model validation also rejects mismatched givens, outer scalar answers, role/unit metadata, fake constants, unrelated simultaneous model families, and visual answer leaks. Scoped worked-solution rules track symbolic origins from the actual givens and permitted quantities: unrelated true arithmetic, data-free answer identities and wrong units do not count as work. Combined and split teacher calculations remain supported. Existing grading for other profiles is unchanged.

Standalone checks:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --practical-open
dotnet run --project UnitTest/QuizPdfTests -c Release -- --practical-open artifacts/verification/practical-open/pdf
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --practical-open-model "<model.gguf>" artifacts/verification/practical-open/native
```

The PDF suite covers 300 factory/mode cases and produces 12 bilingual worksheet/answer PDFs. It checks independent arithmetic, exact operands, localized integer formatting, units, visuals, quarantine isolation and immutable worksheet exports. The native command requests five targets in Vietnamese and English and records every attempt, disposition, prompt, timing and raw JSON. See [verification report](../artifacts/verification/practical-open/REPORT.md) for measured results and editorial limits.
