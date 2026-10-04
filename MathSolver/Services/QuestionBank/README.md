# Local AI question templates

The practice engine owns arithmetic, random operands, answer choices and grading. Local AI supplies reusable prose templates and a prose solution lead. Selecting a question remains an independent 50/50 choice between a fresh C# question and the optional SQLite bank. Empty, inaccessible or invalid bank entries fall back to the C# generator.

Local GGUF prompt processing and token decoding use `max(1, floor(Environment.ProcessorCount * 0.75))` worker threads: 12 logical processors means 9 workers. This limits the configured inference thread count, not operating-system CPU utilisation; other app/native work and hardware scheduling can affect measured CPU usage.

## Version 3 addition: separate relationships and topics

New addition jobs use version 3. C# selects a mathematical relationship independently of the story setting; AI supplies only the prose and a compatible countable-object unit ID. Subtraction, multiplication and division continue to use version 2. Existing version 1/2 bank rows remain readable.

| Stars | Available addition relationships |
| --- | --- |
| 1 | Increase/arrivals, combine independent groups, activity results or spatial parts |
| 2 | The above, plus find the larger amount from a known amount and a positive difference |
| 3–5 | The above, plus recover an initial amount after removal, and find a larger amount when the known amount is described as fewer |

These are **one-step, two-operand** templates, not a complete grade 1–5 syllabus or multistep reasoning progression. C# chooses a viable scene and star-specific scale **before** generating values. An incompatible small scene is excluded rather than rerolling a high-star primary into a smaller digit bucket.

| Stars | Primary operand | Typical scale |
| --- | --- | --- |
| 1 | 1–9 | Personal belongings, small groups, short activities, reading corners |
| 2 | 10–99, within the scene's capacity | Collections, classes, small clubs, craft groups, stalls |
| 3 | 100–999 | Schools, school libraries, workshops, farms, nurseries, bookshops |
| 4 | 1,000–9,999 | School clusters, district libraries, wholesale stock, weekly production |
| 5 | 10,000–99,999 | School networks, city libraries, distribution centres, factories and monthly production |

`AdditionQuestionScales` binds actor patterns, compatible object units, spatial/time labels, operand caps and combined-quantity capacities to each scene/tier. For example, club arrivals are available only at stars 1–2 and total at most 30 participants. Family belongings, sports scores and arriving birds are also excluded at stars 3–5. A large garden scene counts **seedlings** in nursery rows/sections, not tens of thousands of mature trees in a household garden. Notebook production uses days at star 3, weeks at star 4 and months at star 5. Counts belonging to different periods/groups must not overlap or be cumulative totals containing one another.

The primary retains the selected digit bucket. The secondary may use any lower bucket up to the selected tier; the result can cross the primary's digit boundary. Carrying is varied, with a preference for one, two and three carry positions at stars 3, 4 and 5 respectively. Sampling is bounded and never violates the quantity domain in pursuit of a carry target. The plain C# arithmetic generator and grading rules are unchanged.

Fourteen settings span family, school, community, environment, activities, nature and shopping: gifts, libraries, school supplies, donations, recycling, crafts, notebook production, book distribution, harvesting, garden rows/sections, sports halves, arriving birds, club arrivals and shop inventory. Their availability, actors and units depend on the tier. A shop story counts objects; it does not introduce money. Measurement, money and additional solution steps require separate fact models.

Version 3 contracts add `TopicId`, `SceneId`, `PartA` and `PartB`. Roles can be people, teams or places. For two periods/spatial parts, the same actor/location owns both operands; `{part_a}` and `{part_b}` preserve the C#-selected period/part roles. For independent groups, `{name}` and `{other}` bind different groups. For comparison, `{a}` is the known amount of `{other}`, `{b}` is the difference and `{name}` is the amount requested. C# regenerates actors and numbers at each use without changing these roles.

```json
{
  "given_a": "Trong {part_a}, {name} gấp được {a} {unit} để trang trí,",
  "given_b": "đến {part_b}, {name} làm được {b} {unit}.",
  "question": "Qua các buổi làm thủ công, {name} làm được tất cả bao nhiêu {unit}?",
  "solution_lead": "Tổng số {unit} làm được qua các buổi là:",
  "unit_id": "paper-flowers"
}
```

The five-field output schema stays the same. Version 3 placeholders are `{a}`, `{b}`, `{name}`, `{other}`, `{unit}`, `{part_a}` and `{part_b}`. Time/place/activity scenes permit varied openings; comparison facts keep the known actor as the active subject, while the wording remains generated. Validation checks the selected setting's activity, actor/period roles, comparison direction, target and compatible units, including the solution lead. It does not force every story to express possession or receiving. Generated prose still needs bounded semantic rules; this is not unrestricted natural-language validation.

Generation balances relationships, settings and topics with a bounded per-selection history. An unused setting precedes its broad topic so, for example, library usage cannot starve school supplies. SQLite selection first balances semantic buckets, then selects a row within a bucket. A topic with many saved phrasings cannot dominate simply because it has more rows. `LastUsedUtc` persists usage across store/app restarts. Template hashes include topic/setting/relationship but exclude preview operands and actors.

The version 3 schema and Excel columns are unchanged. The same scale checks apply at generation, insertion/import, practice selection and export. Previously saved version 3 contracts that violate the current actor, unit, period or quantity policy are skipped, not deleted or silently rewritten into a different setting. Valid templates retain their scene, tier, unit and period roles when C# refreshes their facts. Legacy version 1/2 formats retain their existing compatibility path.

SQLite adds `Structure`, `TopicId` and `SceneId` as queryable columns and a selection index. Existing tables receive these columns automatically without deleting saved questions. Version 3 indexed metadata must agree with `ContractJson`; invalid edits are skipped during practice and export. Old schemas and legacy deduplication remain supported.

### Addition prose field boundaries

The addition prompt gives each field a separate task: `given_a` states one fact
and ends with a comma, `given_b` continues in lowercase and ends with a period,
`question` asks only for the target, and
`solution_lead` introduces the pupil's calculation without solving it. Only the
selected setting's activity and one role example are supplied; verbs from other
settings are not mixed into the instructions. `{unit}` represents the complete
object noun, so appending a literal noun to it is invalid.

Native grammar binds actors early, keeps quantities in their own givens, and
allows a single clause per given and one sentence for the question/lead. The question uses a normal target-first
Vietnamese / unit-first English order, rather than arbitrary slot permutations.
Production validation also rejects appended explanations, repeated actor/unit
slots and worked answers, including imports that bypass native generation. Older
comparison templates with an introductory `{unit}` remain readable.

Arrival templates bind `{name}` as a location, not as a bird or participant.
Rows belong to the named garden, and activity results belong to the named actor;
appending an otherwise unconnected actor/location slot does not satisfy these roles.
Contribution comparisons keep the same activity in both givens: an amount
collected/donated cannot silently become an amount merely owned. The native
comparison clause uses the selected activity; validation still accepts the
catalogue's supported synonyms.

Live GGUF checks also guard against literal Unicode digits and doubled actor
roles such as `{name}'s craft group` when `{name}` already names the group.
Addition grammar permits Vietnamese/English Latin prose and clause punctuation,
with quantities confined to placeholders. Validation accepts natural combined-period
questions without requiring the word `total`, and past-tense removal/original-stock
phrasing, while rejecting partial-period targets and dangling conjunctions.
Original-stock questions may ask what the actor `started with`. The native second
fact in this relation must use an explicit removal phrase; ambiguous ownership or
`took` alone cannot replace it. Stock facts also reject orphaned letters before a
quantity. Display formatting removes duplicate commas and spaces before punctuation,
while retaining original saved JSON and hashes.

Streaming previews and stored templates use the same clause formatter. Old
period-separated givens also display with a comma and lowercase continuation;
proper names keep their spelling. Raw saved JSON and hash input are not rewritten.

The short-fact / short-question style was reviewed against
[Con Tu Hoc's grade 1 word-problem collection](https://www.contuhoc.com/bai-toan-co-loi-van-lop-1).
This is a supplementary exercise source, not a claim of official curriculum
approval. Multi-step examples and problems needing heterogeneous object groups
are not added to the two-operand schema.

## Version 2 format (other operations and existing templates)

`ContractJson` records the operation, stars, language and C# mathematical structure. Its operands and actors are preview values; they are regenerated during practice using the existing curriculum. `DraftJson` stores five string fields:

```json
{
  "given_a": "{name} có {a} {unit}.",
  "given_b": "{name} vừa được tặng {b} {unit}.",
  "question": "Hỏi {name} có tổng cộng bao nhiêu {unit}?",
  "solution_lead": "Tổng số {unit} mà {name} có là:",
  "unit_id": "books"
}
```

Allowed placeholders are `{a}`, `{b}`, `{name}`, `{other}`, `{unit}`, `{group}` and `{group_one}`. Actor values include any title; the model must not add gendered pronouns or titles to the slots. `unit_id` selects a C# catalogue entry with compatible object and container units. English rendering handles singular quantities and single containers. When a generated group contains more than 100 objects, C# uses warehouse-sized groups rather than putting thousands of items in a small box; unit selection remains independent of the numeric values seen only by C#.

Eleven one-step structures cover increase, combining holdings, recovering an initial amount, remaining stock, differences, missing parts, equal groups, multiplicative comparison, equal sharing, counting groups and finding the smaller amount from a multiplicative comparison. They use the existing four integer operations. Higher stars unlock additional relations while preserving curriculum operand buckets. These are skill difficulty levels, not an equivalence to school grades. Multi-step stories require a separate C# step contract and are not accepted by this schema.

The validator checks JSON fields, bounded text, placeholder counts and roles, approved unit families, operation relationships, the requested quantity and the prose solution target. Digits, numerical answers, formulas, unsupported slots, extra spelled-out quantities and negation are rejected. Validation is a bounded language-rule check, not a complete natural-language theorem prover. Add accepted paraphrases and contradictory examples together when expanding the language rules.

Native GBNF constrains output to the five fields, known placeholders and catalogue IDs. It does not constrain prose to the example's exact sentences. Streaming previews render provisional slots from the C# preview contract; a unit choice arriving later can update the display. Partial or rejected output is never inserted. Retry limits, background generation, cancellation and developer diagnostics remain unchanged.

SQLite keeps the existing `BasicQuestionBank` table. Version 1 fixed-fact rows remain readable and retain their original values. Deduplication of templates excludes random preview numbers and actors. Excel exports now have 25 columns: the original 17, version 2's `Structure`, `OtherSubject`, `SolutionLead`, `UnitId`, and version 3's `TopicId`, `SceneId`, `PartA`, `PartB`. Old version 1/2 workbooks still import. Numeric answers in imported workbooks never override C# calculations.

## SQLite inquiry and Excel interchange

The advanced inquiry panel accepts one `SELECT`, `INSERT`, `UPDATE` or `DELETE` statement, including a `WITH` clause and SQL comments. Multiple statements, DDL and attaching databases are not supported. A write runs in a transaction and rolls back on errors, cancellation or timeout. Inquiry results show at most 100 rows, 32 columns and 4,000 characters per cell; the query timeout is three seconds. The table is read-only; editing is performed through SQL or Excel import.

```sql
SELECT Operation, Stars, Language, Version, DraftJson, UseCount
FROM BasicQuestionBank
ORDER BY CreatedUtc DESC LIMIT 50;
```

```sql
SELECT Stars, TopicId, SceneId, Structure, COUNT(*) AS Templates
FROM BasicQuestionBank
WHERE Operation = 0 AND Version = 3
GROUP BY Stars, TopicId, SceneId, Structure;
```

`Operation` uses 0 = addition, 1 = subtraction, 2 = multiplication, 3 = division; `Language` uses 0 = Vietnamese, 1 = English. `Structure` is the numeric enum value: addition uses 0 = Increase, 1 = Combine, 2 = RecoverInitial, 11 = AddComparisonMore, 12 = AddComparisonInverse. `CreatedUtc` and `LastUsedUtc` are UTC .NET ticks. `Hash` is the primary key. Direct SQL edits are checked again before a record can be used in practice or exported.

Export the entire bank as `.xlsx`, or export an empty bank to obtain column headers. Existing version 1 files need the original required columns: `Version`, `Operation`, `Stars`, `Language`, `Left`, `Right`, `Subject`, `Unit`, `GroupUnit`, `GivenA`, `GivenB` and `Question`. For version 2 also supply `Structure`, `OtherSubject`, `SolutionLead` and `UnitId`. Keep `Left`, `Right` and actors valid for the preview; practice regenerates them. Operations and structures use their C# enum names; language uses `vi-VN` or `en-US`. The three problem clauses and solution lead contain placeholders, not preview values. Units must match the selected `UnitId`.

For version 3 also provide `TopicId` and `SceneId` from the addition catalogue. `PartA` and `PartB` must match the scene's periods/rows, or be empty for scenes without those roles. These fields are retained on export/import; setting, activity and units must agree. Do not turn an existing version 2 row into version 3 just by changing its version number.

`ModelName`, ISO 8601 `CreatedUtc` and `RawJson` are optional import metadata. `Answer` and `ProblemText` are export previews and are not trusted as mathematical facts. Import skips duplicate/invalid rows and reports row errors. Accepted inserts commit together; cancellation or a database failure rolls back the import. Limits are 20 MB compressed, 40 MB expanded and 10,000 imported rows. Data columns must contain values rather than Excel formulas.

## Name data attribution

The Vietnamese pools contain exactly the first 100 given names in each frequency list, preserving the published order:

- Nguyễn Đức Anh, **300 tên nam giới thường gặp ở Việt Nam**, VNTH01: <https://hoten.org/300-ten-nam-gioi-thuong-gap/>.
- Nguyễn Đức Anh, **300 tên nữ giới thường gặp ở Việt Nam**, VNTH01: <https://hoten.org/300-ten-nu-gioi-thuong-gap/>.
- License: [Creative Commons Attribution 4.0 International](https://creativecommons.org/licenses/by/4.0/). Adaptation: extract the first 100 names, omit counts/percentages and combine them with independently selected fictional roles. Accessed 2026-10-04.

Names occur in both lists; these frequency pools do not establish an exclusive gender rule. A fictional actor's role is selected separately from the chosen pool. English given-name examples are based on the US Social Security Administration's [popular names over the last century](https://www.ssa.gov/oact/babynames/decades/century.html); they are not a universal list for every English-speaking country.

Structure references: the Vietnamese [2018 mathematics curriculum](https://boiduonghanoi.edu.vn/pluginfile.php/45/mod_folder/content/0/3-CT-Toan.pdf), [Common Core operations and algebraic thinking](https://www.thecorestandards.org/Math/Content/OA/), and England's [primary mathematics programmes of study](https://www.gov.uk/government/publications/national-curriculum-in-england-mathematics-programmes-of-study/national-curriculum-in-england-mathematics-programmes-of-study). Example sentences in the code are original; no textbook exercises are copied.

Run `dotnet run --project UnitTest/AiQuestionBankTests` for template, legacy, SQLite, Excel, streaming and worker regression checks. See that project's README for the optional live GGUF validation run.
