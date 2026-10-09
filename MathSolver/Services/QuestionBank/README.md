# Local AI question templates

Reviewed story wording and scene metadata are loaded from the bundled quiz JSON
catalogue. See the [authoring guide](../../QUIZ_CONTENT_AUTHORING.md) for language
packs, context lists, placeholders and validation commands. Arithmetic rules and
AI grammar validation remain in C#.

Measurement word problems now cover mass, capacity and length conversions,
including additions/removals at higher star levels. See
[MEASUREMENT_AI.md](../../MEASUREMENT_AI.md) for supported profiles, JSON authoring,
exact answers, prose deduplication and real-model verification commands.

The practice engine owns arithmetic, random operands, answer choices and grading. Local AI supplies reusable prose templates and a prose solution lead. For a selected arithmetic, Find X or fraction family, the practice-style picker chooses numeric exercises (the default) or word problems. Numeric mode retains the original C# generator and does not read SQLite or use a knowledge group. Word mode shows all nine knowledge groups and independently selects reviewed C# stories or optional SQLite templates 50/50; empty, inaccessible or invalid bank entries fall back to C# stories, never numeric exercises. Neither practice source runs model inference. The global mixed-topic option keeps its previous random numeric/story behaviour.

## Choosing a practice style

- **Basic arithmetic → Arithmetic practice style:** Numeric exercises / Word problems.
- **Find X → Find X practice style:** Find X exercises / Word problems.
- **Fractions → Fraction practice:** Numeric exercises / Word problems.

Each family remembers its own style while switching families during the page session. The shared picker, knowledge-group selector and collapsed settings summary update in Vietnamese and English on Windows and Android. Arithmetic/fraction comparison uses its existing generator and hides these selectors. Word mode reuses the existing validated banks, fresh C# facts, star policies and prose deduplication; no database migration or additional model loading is needed.

## Two-number, average, percentage and multi-step templates (version 8)

Motion templates also use version 8 (`ProblemType = 7`), covering the existing
basic, meeting, chasing and river generators. C# retains the actors, physical
unit profiles, directions, rest-time arithmetic, answers and grading. Reviewed
wording comes from `Lists.Narrative.Motion.Phrasings`; native grammar excludes
saved prose before sampling. Practice replaces facts using the same schema and
falls back to C# when the bank has no valid matching template. See
[motion authoring and real-model verification](../../MOTION_AI.md).

Multi-step word problems now share the version 8 projection, storage, Excel and
practice path (`ProblemType = 6`). All four existing multi-step subtypes retain
their C# mathematical roles, chronology and inference steps. Reviewed Vietnamese
and English alternative clauses live in `Lists.Narrative.MultiStep.Phrasings`.
Native sampling excludes used complete fact/question combinations before model
generation, and validation/storage enforce them again. See
[the multi-step authoring and verification guide](../../MULTISTEP_AI.md).

The AI supplementation page also offers sum/difference, sum/ratio and
difference/ratio; the six existing average subtypes; and percentage ratio,
percentage value and whole-from-percentage questions. These use the existing
1–5-star C# curriculum generators, in Vietnamese and English. They do not
change numeric arithmetic, Find X or fraction practice.

`ReasoningStoryCatalogue` projects one generated C# situation into a reusable
template. `QuizNarrativeCapture` records numeric arguments and nested translated
fragments, preserving each quantity's role even when several values coincide.
Number slots (`{f0}`, …), owner/unit slots (`{v0}`, …), the question and ordered
solution steps belong to C#. The LLM receives only that selected situation, a
role map and one JSON example:

```json
{
  "facts": [{ "role": "fact_0", "text": "…{f0}…{v0}…" }],
  "question": "…{v0}…?",
  "solution_leads": [{ "step": "step_0", "text": "…{v0}…" }]
}
```

Facts and solution leads are lists, not a forced two-given structure. Average
questions retain intermediate totals and all their calculation steps. The model
does not supply equations or answers. Preview streams partial fact prose;
completed practice uses the existing diagrams, answer choices and essay grader.

`ReasoningStoryValidator` checks exact role/step lists, variable order, owners,
dimensions, mathematical relation and target against the selected C# situation.
`SemanticProseRules` permits neutral linking words and defined equivalent
synonyms around ordered semantic anchors. Native grammar binds JSON shape,
ordered variables and the remaining role/unit/relation phrases. It composes
phrase substitutions using the same equivalence groups as the validator,
instead of allowing arbitrary text between variables. This allows controlled
equivalent wording, but is deliberately conservative: it is not a
general natural-language meaning classifier, and unrestricted rephrasing can
be rejected. Foreign text, concrete numbers, extra conditions and changed
targets remain invalid. Applied, Find X and fraction templates use the same
anchor policy in place of exact sentence equality. Developer logs name the
failing fields or fact/step roles. See [structure verification](../../AI_STRUCTURE_VERIFICATION.md)
for a reproduced three-attempt failure and the real-model regression matrix.

SQLite records the language template and a `ReasoningStorySeed` with a schema
fingerprint. Family IDs 3/4/5 identify two-number/average/percentage questions;
`ProblemVariant` identifies their existing subtype. On reuse, C# chooses a new
seed with the same context, role and step schema, then recalculates every answer
and step. A retired or incompatible schema falls back to the original C#
question. Practice keeps its independent 50/50 C#/SQLite choice; inference is
never run to serve a practice question. Recent context history reduces repeated
generation and selection. Excel adds optional `StorySeedJson`, `FactsJson` and
`SolutionLeadsJson` columns; previous workbook versions still import.

The default AI test suite covers 640 bilingual subtype/star/seed combinations,
fresh facts, all answer modes, essay grading, mutation rejection, streaming,
SQLite, Excel and background generation. Real Gemma 4 E4B Q4_0 verification
accepted a three-star sum/difference, four-star indirect average and five-star
whole-from-percentage template (average needed a second attempt). Logs are in
`artifacts/verification/reasoning-model-20261007-160045`. The earlier three-family
check of 480 prompts including correction instructions found a maximum of 631 input
tokens (976 with two rejected wording examples), within the existing 2048-token
context and 700-token output reservation.

## Shared one-step practice catalogue

### Knowledge groups and star difficulty (version 5)

In **Basic arithmetic → Word problems** practice and AI supplementation, select **Knowledge group** and 1–5 stars. Both screens initially select quantities and comparison as the knowledge group. There is no grade selector, grade-based group filtering or grade-based operation restriction. All nine groups and all four operations are available. The profile applies to the four basic operations; the other puzzle families keep their own curricula.

| Group | Selected C# facts and supported questions |
| --- | --- |
| Quantities and comparison | People, animals, pages, points, visits, seedlings, recycling, baking and harvests; independent totals, comparisons and equal activity groups |
| Money and transactions | Purchases, savings, remaining money and price per item |
| Time and age | Age differences and inverse comparison; successive activities, remaining time, repeated daily durations and duration per day |
| Measurement | Mass, length, transport loads, water capacity; kg/g, km/m and l/ml conversion |
| Practical geometry | Areas of separate plots, remaining area, rectangular area, missing side, perimeter from two sides, tank volume from base area and height, missing height |
| Grouping and packing | Packing progress, equal packs, exact sharing, counting packs; remainders from 3 stars and minimum packs from 4 stars |
| Production and simple rates | Independent shifts, remaining target, equal output per shift and unitary output; recovering total dispatched output at 5 stars |
| Data and averages | Two-category survey totals/differences, repeated daily visits, average from total and days, total from a given average from 4 stars; a factual table is shown in practice and AI preview |
| Motion | Successive/remaining distances, distance from constant speed and time, speed from distance and time |

`AppliedQuestionCatalogue.Contexts.cs` adds these reviewed contexts without putting the catalogue into an AI prompt. A selected scene binds activity, unit, scale and question target. A classroom is capped at 40 pupils, ages at 90 years, practice at 60 minutes per day, water cans at 20 litres, notebook packs at 100 items and tank height at 5 metres. These are scenario limits, not school-grade labels. High stars can select small-scale inverse relationships as well as larger counts. Practical perimeter, remainder and minimum-pack questions have explicit algorithm-owned answer rules; they never use a truncated quotient as the answer to a rounding question. A numeric C# exercise keeps ordinary arithmetic when its paired scene needs a derived answer rule.

`QuestionLearningProfile` selects a knowledge group and star policy, `AppliedQuestionCatalogue` binds units, quantity roles and context capacity, and `AppliedQuestionCycle` rotates eligible scenes with a bounded history. They supply both numeric C# exercises and reviewed C#/AI stories:

| Stars | Count ceiling before context limits | Maximum multiplier/divisor before context limits |
| --- | --- | --- |
| 1 | 9 | 5 |
| 2 | 99 | 9 |
| 3 | 999 | 9 |
| 4 | 9,999 | 20 |
| 5 | 99,999 | 99 |

For ordinary addition/subtraction, one star has no carrying/borrowing; later stars prefer one through four carry/borrow positions when the context allows them. Higher stars unlock inverse stock/comparison/age questions and conversions. Small pencil/card collections remain eligible at high stars; the ceilings are upper bounds, not mandatory stock sizes. Ordinary division constructs exact multiples; explicit packing remainder/rounding scenes instead require a nonzero remainder and use their dedicated answer rule. Money has its own scale: Vietnamese total ceilings are 20,000 / 50,000 / 100,000 / 300,000 / 500,000 đồng before scene capacity, and English totals have ceilings of 10 / 30 / 100 / 200 / 300 dollars. Notebook prices remain 3,000–30,000 đồng or 1–15 dollars, with separate item-count limits. Bags, truck loads and journey lengths have separate capacities. Uniform motion binds realistic distance, speed and time rather than treating speeds as object counts. At 4–5 stars it can give travel time in minutes, converted to working hours; five-star measurement can present decimal kg/km/l with exact integer g/m/ml answers.

The version-5 prompt contains only the selected scene, its dimensions and short reviewed surface examples; it includes no numerical facts, personal names or full catalogue. Grammar binds ordered variables; validation checks ordered semantic anchors with defined synonyms and neutral links, as described above. Extra quantities, changed units/roles/targets and foreign prose fail validation. Conversion steps are generated by C# and shown in the worked solution. Written submissions for these problems preserve all calculation lines, check exact kg/g, km/m or hour/minute conversions and intermediate arithmetic, then check the final value/unit. They accept a correct alternative arithmetic method and reject an incorrect conversion even when the final answer happens to match.

SQLite selection matches group/operation/stars/language before revalidation. Persisted enum IDs remain stable: Objects=0, Money=1, legacy Mass=2/Length=3/Transport=4, Motion=5, Time=6, Measurement=7, Geometry=8, Packaging=9, Production=10, Data=11. The new Measurement picker also reads historical mass/length/transport rows. The `Grade` column is retained only for historical database/Excel compatibility; new facts use zero and it does not partition selection or constrain fresh operands. Saved version-5 templates from different grades share the same group pool. Version 1–4 counted templates remain available under quantities and comparison, without needing a separate general selector. Old Excel files still import. Word mode retains the independent C#/bank 50/50 choice. Only global mixed-topic practice retains the C# numeric/story 50/50 choice. Model inference remains outside the practice request.

The relation and measurement types draw on the [2018 Ministry mathematics curriculum (PDF mirror)](https://lophocnguvan.com/wp-content/uploads/Giao-duc-pho-thong-2018-Mon-Toan.pdf); app stars are skill levels rather than grade classifications. This implementation covers natural-number one-step arithmetic and selected measurement conversions. General fraction/decimal answers, percentages and multistep monetary dependencies remain with their existing dedicated C# engines; they are not silently added to this bank schema.

`OneStepQuestionCatalogue.All` holds a reviewed list of scene/relationship/star patterns, not finished questions with fixed numbers. `OneStepQuestionCycle` selects a relationship and a compatible setting with bounded history, creates fresh facts through the same addition/arithmetic catalogues used by AI, and selects a reviewed wording variant. Both sources use `BasicQuestionContract.ToPracticeQuestion` for true/false, multiple-choice and essay answers. C# templates also pass the AI bank's validator in tests.

Both arithmetic practice styles remain available when the bank is empty or model inference is unavailable. The numeric branch returns the original generated question intact, including its operands, answer choices and grading behavior. Word mode always serves a story, from C# or validated SQLite. Comparison exercises and the other mathematical families keep their existing generators. The built-in branch does not access SQLite. No built-in patterns are inserted into the user's database.

| Operation | Shared one-step relationships |
| --- | --- |
| Addition | Increase/arrivals, combine independent groups/periods/parts, find the larger amount, recover original stock, inverse additive comparison |
| Subtraction | Remaining stock, difference, missing amount, find the smaller amount, inverse additive comparison, find one part from a combined total |
| Multiplication | Equal groups, multiplicative comparison |
| Division | Equal sharing, count groups, find the smaller amount from a factor, compare two amounts to find the factor |

New version-4 enum values are appended, preserving all previous stored structure IDs. Finding the smaller additive amount and finding a part unlock at two stars; inverse additive comparison and finding a factor unlock at three stars. Their primary operand keeps the existing digit bucket and context capacity. The factor-comparison answer is measured in Vietnamese `lần` or English `times`, rather than the counted object's unit. Legacy version-2 relation eligibility is unchanged.

Extended relationships use explicit actor/quantity/target checks, and native grammar offers reviewed alternatives for their relation clauses. Changing comparison direction, asking for the other actor, hiding another relation in a clause or changing the solution target is rejected. The prompt contains only the selected context and one shared role example. It does not send the complete pattern list or preview operands to the model.

The legacy patterns below remain one-step counted-integer problems. Knowledge-group money and measurement use version 5 above. Fractions, general decimal answers and multistep dependencies use separate engines.

Local GGUF prompt processing and token decoding use `max(1, floor(Environment.ProcessorCount * 0.75))` worker threads: 12 logical processors means 9 workers. This limits the configured inference thread count, not operating-system CPU utilisation; other app/native work and hardware scheduling can affect measured CPU usage.

## Question wording deduplication

`QuestionProseIdentity` compares the question's given clauses and target question,
independently of C# random numbers, actor names, star/grade/scene metadata and the
solution lead. It renders fixed context/object/unit slots, normalizes Unicode, case,
spacing and punctuation, then hashes that normalized wording. Different wording
can reuse the same context and units; this is exact normalized wording detection,
not a semantic similarity classifier. The original row hashes remain unchanged.

The generation worker checks SQLite and accepted items in the current batch before
offering or saving a question. A duplicate triggers a revised prompt containing the
rejected wording; the C# contract stays the same. Validation and duplicate retries
share a maximum of three attempts per item. An exhausted duplicate stops the batch
with `DuplicateProseRetriesExhausted`; partial prose is never saved. Automatic and
manual saves and Excel imports also check wording at the storage boundary.

For applied knowledge-group, Find X and fraction templates, the prompt prefers
a concrete unused example when available. Exhausting that finite example list
no longer exhausts grammar: new equivalent prose can be generated around the
same semantic anchors. Duplicate detection happens after validation and at
insertion, including concurrent inserts, with the existing three-attempt bound.
Solution leads do not create novelty. Historical rows are retained.

The derived index carries a validation revision so a policy upgrade revisits
previously cached invalid rows without changing public question records.
See [real-model verification](../../AI_PROSE_DUPLICATE_VERIFICATION.md) for the
reproduced three-identical-output failure and the subsequent eleven successful
model-generated questions with separate SQLite databases.

`QuestionProseIndex` is a separate derived SQLite index, backfilled lazily for existing
valid records. SQL/grid insert, update and delete triggers invalidate affected index
entries. Historical duplicate rows are retained, and invalid records do not block
valid generation. Inference remains outside database operations and practice requests.

## Version 3 addition: separate relationships and topics

New addition jobs use version 3. C# selects a mathematical relationship independently of the story setting, then selects a compatible countable-object unit. AI supplies only the prose and returns the selected unit ID. New subtraction, multiplication and division jobs use version 4, described below. Existing version 1/2/3 bank rows remain readable.

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

The primary retains the selected digit bucket. The secondary may use any lower bucket up to the selected tier; the result can cross the primary's digit boundary. Carrying is varied, with a preference for one, two and three carry positions at stars 3, 4 and 5 respectively. Sampling is bounded and never violates the quantity domain in pursuit of a carry target. The arithmetic engine and grading rules are unchanged; basic practice now uses the reviewed prose catalogue for its C# source.

Twenty-nine settings include the original fourteen plus reading, school furniture, food stock, baking, poultry, fish farming, cattle, crop harvesting, product manufacturing, passenger visits, vehicle passages, building materials, planting, survey responses and experiment outcomes. Their availability, actors and units depend on the tier.

| Context group | Supported addition quantities |
| --- | --- |
| School | Books, supplies, pages read, desks and chairs |
| Food | Fruit, cakes, bread rolls, cartons of milk and bags of rice |
| Animals | Chickens, ducks, fish and cattle |
| Agriculture | Mangoes, rice sacks, seedlings and harvested fruit |
| Trade | Product counts, stock and production across separate periods |
| Traffic | Passenger visits and vehicle passages in separate periods |
| Construction | Bricks and tiles held in appropriately sized material stores |
| Environment | Planting results, seedlings and recycling collections |
| Statistics | Responses from separate, nonoverlapping survey samples |
| Probability experiments | Counts of the same outcome in separate coin/die trials |

These are addition **counts**, not revenue, discounts, distance/speed, mass, temperature, area/volume, averages or probability calculations. Milk cartons and rice sacks are counted packages, not converted measurements. Passenger visits count visits rather than unique people or simultaneous occupancy. Large experiment counts use simulations, and large animal counts use farms/networks instead of home pens. Money, measurement and additional solution steps require separate fact models.

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
selected setting's activity, actor scale, relation, one selected unit and one role example are supplied; verbs from other
settings are not mixed into the instructions. `{unit}` represents the complete
object noun, so appending a literal noun to it is invalid.

The prompt never includes the entire scene/unit catalogue. C# random selection and
balanced recent-use history supply diversity without adding unrelated choices to
each generation. Addition grammar binds the selected unit ID; import validation
continues to accept compatible units from the same scene/tier. Inference uses the
LLamaSharp `Basic` grammar optimisation: validate the sampled candidate first,
falling back to full-vocabulary grammar filtering when needed. Both paths enforce
grammar; C# semantic/language validation remains required before insertion.
English contribution facts bind one of the selected scene's affirmative verbs
before the quantity; questions and solution leads remain generated prose.
Validation also rejects English contracted negation (`don't`, `doesn't`, curly
apostrophe variants) in every field, including imported/historical templates.

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

## Version 4: subtraction, multiplication and division

`ArithmeticQuestionCatalogue` reuses 22 bilingual countable-stock settings and the
star-specific actor profiles. The operation determines the activity: lending books,
selling stock, handing out supplies, transferring seedlings or recycling collected
bottles. Completed harvests and manufactured goods are now stock; plants awaiting
planting are distinct from those already planted. Visits, scores and experimental
outcomes are not forced into physical-container stories.

The primary operand retains its digit bucket: 1–9, 10–99, 100–999, 1,000–9,999,
and 10,000–99,999 for 1–5 stars. A scene can impose a tighter limit without allowing
a primary value below the tier minimum. Small personal collections are excluded
at large scales. Subtraction keeps the removed/available amount within the initial
stock and prefers borrowing at higher tiers. Zero remaining stock is allowed.

Multiplication uses a factor/group count from 2–9 and bounds the total by twice the
primary bucket maximum and the context capacity. Its owner profile is planned one
scale larger (up to five stars) to accommodate the product. Division constructs an
exact multiple inside the primary bucket; it never retries down to a tiny dividend.
Equal sharing and multiplicative comparison use a divisor from 2–9. Counting groups
uses a per-group quantity chosen so the resulting number of groups is 2–9; the
divisor is not incorrectly interpreted as the group count.

| Stars | Subtraction | Multiplication | Division |
| --- | --- | --- | --- |
| 1 | Remaining stock | Equal groups | Equal sharing |
| 2 | Also missing part | Equal groups | Also counting groups |
| 3–5 | Also difference | Also multiplicative comparison | Also smaller quantity from multiplicative comparison |

Groups depend on the actual objects and per-group amount: boxes/packs for small
goods and stock lots for large goods; flocks or rearing sections for poultry;
herds or farm clusters for cattle; tanks or ponds for fish; rows, nursery sections
or nursery clusters for seedlings; pallets or material lots for bricks. Furniture
above 40 items per group uses stock lots, not classroom occupancy. Fresh practice
numbers can change the group label; templates use `{group}`/`{group_one}` throughout.
Counting-group answers use the group unit; equal-sharing answers use the object unit.

The compact prompt sends only the selected setting, actor scope, roles, units and
one example, without preview numbers/names or the full catalogue. Grammar binds the
numeric placeholders, actors and selected unit ID. Validation checks the existing
mathematical roles plus context compatibility, extra object nouns, negation, changed
units and misplaced actors. It remains a bounded validator, not a complete semantic
proof engine. Generation and SQLite selection balance relationships/settings with
separate bounded histories for each operation, tier and language.

Version 4 uses the existing SQLite/Excel fields; `TopicId` and `SceneId` identify the
setting, while `PartA` and `PartB` are empty. All storage, practice and Excel boundaries
revalidate the contract and prose. Versions 1–3 keep their original interpretation.

## Version 2 format (existing templates)

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

SQLite keeps the existing `BasicQuestionBank` table. Version 1 fixed-fact rows remain readable and retain their original values. Deduplication of templates excludes random preview numbers and actors. Excel exports have 33 columns: the original 17, version 2's `Structure`, `OtherSubject`, `SolutionLead`, `UnitId`, version 3's `TopicId`, `SceneId`, `PartA`, `PartB`, version 5's `Grade`, `KnowledgeGroup`, version 6's `UnknownRole`, version 7's `LeftDenominator` and `RightDenominator`, and version 8's three structured-story JSON columns described above. Old workbooks still import. Numeric answers in imported workbooks never override C# calculations.

## Fraction word problems (version 7)

In practice, select **Fractions → Word problems**, one of the nine knowledge groups,
and an operation or mixed operations. **Numeric exercises** remains the default;
it uses the original C# generator and does not require a knowledge group. Fraction
comparison also retains its original C# path.

The catalogue contains 84 scenes across objects, money, time, measurement, geometry,
packaging, production, data and motion. Every group supports all four operations.
Budgets and completed work refer to the same whole and cannot exceed it. Partial
bread/cakes are valid quantities; full bottles/bags always have integer counts.
Rectangle lengths and widths are ordered, area uses m², and speed × time uses
km/h × hours → km with a realistic walking speed.

Stars describe skills, not school grades. Maximum denominators are 6/12/12/20/30.
One-star sums/differences have equal denominators; at two stars one denominator
divides the other; higher stars permit general common denominators. Four and five
stars additionally unlock perimeter, average and speed/time situations. C# uses
exact rational arithmetic throughout, including distractors, true/false answers
and essay grading. Wrong units and unrelated operands are rejected.

In AI supplementation, choose **Fraction word problems**, a group, operation,
stars and language. AI returns the existing five-field JSON schema with `{a}`,
`{b}`, `{name}`, `{unit_a}`, `{unit_b}` and `{unit}`. It never produces numeric
facts or mathematical answers. The scene fixes the whole, quantity roles and units;
validation and native grammar share reviewed semantic anchors and composable
phrase substitutions. Unused base examples are selected using the existing
prose identity and exclusion set; new numbers/names cannot bypass duplicate
detection. Duplicate output retries remain bounded; an exhausted scene is
reported rather than inserting duplicate or unsafe prose.

The existing SQLite table stores version 7 with `ProblemType = 2`; denominators
live in `ContractJson`. C# practice mixes reviewed built-in stories with validated
stored templates, substitutes fresh rational facts, and falls back immediately if
the bank is empty. It never waits for model inference. Excel adds optional denominator
columns (default 1 for legacy workbooks) and always recalculates the answer on import.
Practice and AI previews display stacked textbook fractions on Windows and Android.

## Find-X situations (version 6)

Word-problem practice and AI supplementation reuse the nine knowledge groups. In practice,
select **Find X → Word problems** to show the group selector; **Find X exercises**
keeps the original equation generator without a group. In AI supplementation,
select **Find X**, a group, sum/difference/product/quotient, stars and an explicit unknown
role. Roles are an addend, minuend, subtrahend, factor, dividend or divisor. C# practice
can rotate eligible roles; AI generation requires a specific role.

`FindXQuestionCatalogue` builds a structured list from the reviewed applied scenes.
Each entry binds its group, setting/activity, known-fact roles, target role, unknown
role and equation operation to the source scene's input/answer units, star eligibility,
capacity, factor limits and bilingual problem/question/solution templates. Selection
chooses an eligible scene first, then C# generates compatible facts. It constructs the
equation, verifies a unique integer solution and substitutes it back into the equality.
For example, a total of 24 books shared equally among 6 boxes yields an unknown factor
in `6 × X = 24`, with **books per box** as the target, rather than the number of boxes.

The selected scene remains the same when practice regenerates numbers and actors.
Word mode independently mixes C# stories and saved templates. Numeric mode always
keeps a C# equation. An empty or invalid bank falls back to a C# story in word mode; practice never
starts inference. Background generation, three attempts, manual/automatic insertion,
streaming previews and model cleanup use the existing worker.

The compact prompt contains only the selected scene, fact/target roles and units,
allowed placeholders, and one JSON example. It contains no preview numbers or actor
names. Version 6 shares version 5's grammar and reviewed phrase compositions:
validation accepts supported wording and binds the quantity/actor/unit roles and target.
It rejects swapped group/per-group values, exposed answers, extra facts, incompatible
units and foreign/corrupt text. This is bounded template validation, not unrestricted
natural-language understanding. Add reviewed alternatives and rejection tests together
when extending wording. Conversions, remainder/rounding problems and compound formulas
are excluded from this one-step integer Find-X catalogue.

SQLite uses `ProblemType` (0 = arithmetic, 1 = Find X, 2 = fraction word problems) and `ProblemVariant` (0 = arithmetic variant via Operation/Structure, 1 = addend,
2 = minuend, 3 = subtrahend, 4 = factor, 5 = dividend, 6 = divisor). Existing rows remain
available as arithmetic. Selection matches family, group, equation operation, stars,
language and validated scene metadata. Excel version 6 requires the matching catalogue
`SceneId`, `KnowledgeGroup`, `UnknownRole` enum name and `Grade = 0`. `Operation` is the
equation's operation; `Left`/`Right` hold the two known values used by the story's solution,
which can use the inverse operation. Do not convert an old row simply by changing its
version or role. ProblemType is derived from the contract version during Excel import.
Installed databases automatically rename `Family` to `ProblemType` and `UnknownRole`
to `ProblemVariant` in one transaction before schema initialization. Values, keys,
payloads, indexes and existing questions are retained. Use the new names in SQL and
grid edits. `ContractJson` and Excel keep their existing field names for compatibility.
`KnowledgeGroup` still represents the knowledge-group selector; `Grade` is retained
only for historical records and is not a difficulty or problem-type selector.

## SQLite inquiry and Excel interchange

Open **Settings → Data management** (below Language in the settings menu). The
separate `Views/Settings/DataManagementPage` owns browsing, row editing, SQL,
Excel import/export and deleting the bank; the AI page owns generation, preview,
saving and sharing a generated question. This destination is available without
developer mode or loading a model. Both pages use the same app-owned store.
The data page uses the Settings/Hardware title bar and theme, a bounded layout
on desktop and wrapping actions on phones. It opens the editable table directly;
SQL and contextual help are collapsed by default. Returning with a draft offers
to discard it, and active database work is cancelled and awaited before returning.
Future user data, scores or achievements can have their own sections/services in
this destination; they should not depend on AI generation or question contracts.

The advanced inquiry panel accepts one `SELECT`, `INSERT`, `UPDATE` or `DELETE` statement, including a `WITH` clause and SQL comments. Multiple statements, DDL and attaching databases are not supported. A write runs in a transaction and rolls back on errors, cancellation or timeout. Inquiry results show at most 100 rows, 32 columns and 4,000 characters per cell; the query timeout is three seconds. Direct single-table SELECTs containing Hash support cell editing and row insertion/deletion; aggregate and ambiguous projections remain read-only. On Windows the result grid uses scrollbar-style mouse dragging: dragging right reveals later columns. Native scrollbars and Android touch scrolling retain their platform behavior.

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

For version 4, choose `TopicId`/`SceneId` from `ArithmeticQuestionCatalogue`, leave
`PartA`/`PartB` empty, and supply preview actors, numbers and group labels compatible
with the operation's scale profile. Do not migrate old templates by changing only
the version field.

Version 5 requires `KnowledgeGroup` (enum name in Excel; stable IDs 0–11 in SQLite), and a matching `AppliedQuestionCatalogue` scene, relation and units. `Grade` may be omitted or zero; historical values 1–5 remain readable as metadata only. `Left` and `Right` hold the working operands; the selected scene may display kg/km/l instead of g/m/ml or minutes instead of hours. Its conversion policy supplies the mapping. Perimeter/remainder/minimum-pack answers are recomputed from the selected C# scene when importing; spreadsheet answers never override them. Do not change only the version or profile columns on an older template; its roles and units must agree too.

`ModelName`, ISO 8601 `CreatedUtc` and `RawJson` are optional import metadata. `Answer` and `ProblemText` are export previews and are not trusted as mathematical facts. Import skips duplicate/invalid rows and reports row errors. Accepted inserts commit together; cancellation or a database failure rolls back the import. Limits are 20 MB compressed, 40 MB expanded and 10,000 imported rows. Data columns must contain values rather than Excel formulas.

## Inline SQL row insertion and editing

The data page opens the table directly. **SQL** reveals the compact query editor;
**Open table** restores the editable view after a custom query.
**Help** is collapsed by default, with one topic visible at a time: SQL,
table actions, columns or limits. Short table hints follow the current state
(viewing, inserting, editing or read-only), rather than repeating a full manual.
Open table is disabled while a draft or database operation is in progress.
All captions and help use the localization JSON packs, including live language changes.

New SQL-grid rows are entered inline below the column headers, sharing the
table's horizontal viewport. Save/Cancel stay in the table toolbar. The draft
uses the direct SELECT's source columns (including aliases), and omitted columns
use database-editor defaults; use `SELECT *` to enter every column. Failed saves
retain the draft. NULL is explicit, blank Hash generates a key, and CreatedUtc
defaults to the save time if not projected. Existing record identity/conflict
checks and C# practice validation are unchanged.

Clicking a cell only opens its read-only details and Copy/Close actions. Selecting
**Edit row** in the table toolbar expands the selected row into inline editors. The
draft stays associated with the row while CollectionView recycles its views;
aliases for the same source column share a value. Save row uses one parameterized
UPDATE with the full original-row comparison, validating every field before any
write. Failure retains the draft; Cancel discards it. Hash stays read-only. Save,
Cancel and Delete are in the table toolbar. The compact detail panel below is
read-only, shows the selected cell's full draft value and supports Copy; closing
details does not discard row edits. Other rows can be inspected while a draft is
open; saving still targets the original editing row. Short contents use a 48-high viewer; long
contents scroll inside a viewer capped at 128. The table remains capped at 360.

## Clear the question bank

The common question preview exposes **Delete all saved questions** beside Share,
with confirmation in Vietnamese/English. `AiQuestionGenerationService.DeleteAllAsync`
excludes generation and pending saves; `QuestionBankStore.DeleteAllAsync` deletes
all `BasicQuestionBank` rows and their derived prose identities in one transaction.
Cancellation before commit or a SQLite failure rolls back. Selection histories
are reset only after success. Model files and valid preview drafts are retained;
saved previews become Ready so they can be saved again. The page clears stale SQL
results after success. No model load or advanced SQL knowledge is required.

## Name data attribution

The Vietnamese pools contain exactly the first 100 given names in each frequency list, preserving the published order:

- Nguyễn Đức Anh, **300 tên nam giới thường gặp ở Việt Nam**, VNTH01: <https://hoten.org/300-ten-nam-gioi-thuong-gap/>.
- Nguyễn Đức Anh, **300 tên nữ giới thường gặp ở Việt Nam**, VNTH01: <https://hoten.org/300-ten-nu-gioi-thuong-gap/>.
- License: [Creative Commons Attribution 4.0 International](https://creativecommons.org/licenses/by/4.0/). Adaptation: extract the first 100 names, omit counts/percentages and combine them with independently selected fictional roles. Accessed 2026-10-04.

Names occur in both lists; these frequency pools do not establish an exclusive gender rule. A fictional actor's role is selected separately from the chosen pool. English given-name examples are based on the US Social Security Administration's [popular names over the last century](https://www.ssa.gov/oact/babynames/decades/century.html); they are not a universal list for every English-speaking country.

Structure references: the Vietnamese [2018 mathematics curriculum](https://boiduonghanoi.edu.vn/pluginfile.php/45/mod_folder/content/0/3-CT-Toan.pdf), [Common Core operations and algebraic thinking](https://www.thecorestandards.org/Math/Content/OA/), and England's [primary mathematics programmes of study](https://www.gov.uk/government/publications/national-curriculum-in-england-mathematics-programmes-of-study/national-curriculum-in-england-mathematics-programmes-of-study). Example sentences in the code are original; no textbook exercises are copied.

Run `dotnet run --project UnitTest/AiQuestionBankTests` for template, legacy, SQLite, Excel, streaming and worker regression checks. See that project's README for the optional live GGUF validation run.

## Proportion templates

Proportion is story family 8 (Direct=0, Inverse=1). It reuses reviewed narrative grammar, prose deduplication, SQLite/Excel, fresh C# facts and the practice provider. Story.NarrativeId retains the source template and its target. See [PROPORTION_AI.md](../../PROPORTION_AI.md) for supported contexts and language JSON authoring.

## Decimal word problems

Decimal is story family 9, contract version 8. The four arithmetic variants
always generate word problems, retaining Story.NarrativeId for one of eight
continuous-quantity contexts. ExactAnswer uses ReducedFraction; AnswerText
formats the decimal preview. Nonintegral answers must not use the legacy
integer Answer property. SQLite/Excel persist the seed and validated prose;
practice regenerates all numeric facts and exact solution steps. Numeric-only
decimal practice, comparison and rounding are not replaced by bank prose.
See [DECIMAL_AI.md](../../DECIMAL_AI.md) for tiers, JSON authoring and verification.

Packing (family 11) and time (family 12) use reviewed JSON wording. Packing
separates minimum groups, leftovers only and the complete quotient/remainder
tuple. ExactAnswers retains every result; display/grading checks all components
and units. See [PACKING_TIME_AI.md](../../PACKING_TIME_AI.md).

Fraction quantity (family 13) adds finding a fractional part and recovering the
whole, including the existing five-star remainder-based structures. Fifteen
stable contexts carry Count/Mass/Distance/Capacity policies. Reviewed direct
templates and multi-step phrasings are stored in QuizContent JSON. Exact answers,
fresh facts, validation, prose deduplication and SQLite reuse the version 8 story
pipeline. See [FRACTION_QUANTITY_AI.md](../../FRACTION_QUANTITY_AI.md).
