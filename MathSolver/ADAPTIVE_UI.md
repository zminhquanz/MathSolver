# Adaptive practice and solver layout

Basic arithmetic, Find X and fractions share a practice-style picker with the
same responsive border, Android arrow, minimum touch height and accessible title.
Each family starts in numeric mode and remembers its own choice during the page
session. Word-problem mode reveals the nine knowledge groups; numeric and comparison
practice hide them. The collapsed summary includes the selected style and only
shows the group when it applies. Labels and hints update with Vietnamese/English.

Practice settings stay open on entering a fresh session. Questions appear directly,
without a start button. Focusing the essay editor or submitting an answer collapses
the settings into a bilingual summary.
Change settings opens the same controls without regenerating the current question
or clearing work. Existing selection handlers retain their previous behavior when
the user actually changes a selection. Leaving practice resets the collapsed state;
opening settings, diagrams or the AI question bank preserves it.

The AI question-bank action and running-job progress stay in the summary, outside
the collapsible settings. Both Windows and Android can select/import/download a
GGUF model and start a background question-generation job, then return to practice
without waiting for inference or losing their draft. On narrow layouts the two
summary actions share a row below the summary text. The summary toolbar uses
12-by-6 padding, 13-point text and compact bilingual action labels, with full
descriptions for tooltips and screen readers. Buttons have a 40-unit minimum
height on Windows and 48 on Android and grow with text. Actual measured label
widths decide whether actions fit beside the summary, below it, or in separate
rows for very narrow viewports or large text. Hidden progress reserves no gap.

The current optional bank supports basic arithmetic word problems; it does not
restore the former AI practice tab or LLM accuracy benchmark. There is no Windows,
AVX2 or installed-RAM visibility gate on this bank. Android packages the ARM64 CPU
backend. Practice reads committed SQLite templates or immediately uses fresh C#
questions when no matching template exists; it never awaits model generation.
Generation keeps the existing worker budget and releases model weights after each
job. On Android, Home/locking the screen stops generation; navigating inside the
app does not. This is an in-app background task, not a foreground service.

Ordinary solver reading/input pages use a centered viewport-bound layout capped
at 1320 logical units. The entire practice page uses the same 1320-unit cap:
summary, settings, score and question cards share aligned outer edges. Settings
fill the available width, and essay inputs fill the question card's inner width
without separate width caps. Geometry and quadratic calculator plots can use
1440. These are maximum widths, not minimum device requirements.

Practice modes, difficulty, answer choices and diagrams reflow by available
logical content width and system text scale rather than Android/Windows identity.
Diagrams use two columns when at least 920 units are available at normal text size;
enlarged text raises that threshold. Long or multi-answer choices require more
space per column. Resizing and split-screen keep the same question and draft.

Text buttons and picker frames grow beyond their minimum height. Native Android
sub-tab rows also grow, retaining a fixed underline. Windows uses an independent
black/white system keyboard focus outline. Selection has a thicker border; graded
choices have check/cross marks and accessible status descriptions. Fractions expose
a single spoken expression instead of disconnected numerator/denominator labels.
Diagram descriptions use presentation data, preserve unknown values before grading
and include revealed explanations only afterwards.

Automated checks: MathPuzzleUnitTests (including localization, grading and hidden
data), Windows native feedback/sizing harness, Windows and Android builds.
The native sizing harness checks wrapped long text and viewport bindings, without
changing system settings or opening a visible window.

Formula and times-table pages now also have centered, capped content. Formula
subpages share the 1320-unit reading cap; geometry cards and unknown-component
cards choose their column count from available inner width and system text scale.
The Windows Calculation and Formula subtab selectors share a centered 1280-unit
bar, 6-unit gaps, a 48-unit minimum button height and the same text/selection style.
Both keep their subtabs on one horizontal row, with readable widths measured
from the labels and system text scale. Narrow windows scroll sideways with mouse
dragging or touch; selection and resizing bring the selected tab into view.
Android uses the shared native horizontal-tab style with an
underline and scroll-to-selection/swipe navigation. Motion cards, average controls and the
measurement From/Swap/To row reflow without resetting values. Proportion and
motion graph panels use the capped content width rather than the full page width.

Times tables keep CollectionView virtualization, with 1–5 columns selected by
content width and text scale on both platforms. Range selectors also reflow;
the full card accepts a tap and the radio retains keyboard selection with a
48-by-48 target and localized accessible title/hint. Resizing does not rebuild
the tables or change the selected operation/range. Formula diagrams, interactive
plots, sliders and unit-conversion inputs expose accessible descriptions derived
from their current localized labels and values.

Equation and geometry calculators now share the capped solver content width with
the calculation subtab bar. Equation coefficients reflow into 1–3 columns based
on their actual grid width and system text scale; linear mode only reserves two
fields. Geometry selectors, actions and dimension cards follow the same width
policy on Windows and Android. Reflow keeps existing entries and results.

Geometry decides whether to place the diagram beside its formulas from the
preview panel's width, rather than the whole page width. The preview can grow
for longer formulas and large text, with a minimum height matching the adjacent
controls on wide screens. Equation graphs use a width-based height capped at
620 units instead of a fixed 960; zoom out, percentage/reset, zoom in and the 🔍
enlarge action share one row on the right with 48-unit targets. The percentage
appears only on the reset button. Input borders and copy captions can grow with text, and diagram
and coefficient inputs expose localized accessible descriptions.

Geometry dimension cards now use star columns and automatic grid rows rather
than a FlexLayout with a calculated, fixed height. The native entry, border and
padding all contribute to row height, preventing the bottom border from being
clipped after text scaling or a shape change. Zoom controls stay at the right
of equation graphs, wrapping below the heading on narrow screens. The 🔍 button
has a bilingual tooltip and accessible description. Expand graph
opens a modal plot using the existing interactive view; closing it (including
Android Back) restores that view to the calculator and retains its zoom/pan.

SQLite result grids use scrollbar-style mouse dragging on Windows: dragging right
increases the horizontal offset and reveals later columns. Native scrollbar drags
are excluded from the custom pointer handler. Tab strips keep content-following
dragging, and Android retains standard touch scrolling.

Manual verification remains necessary for these device-specific cases:

| Case | Verify |
| --- | --- |
| Laptop, 125/150% display scaling | No horizontal page overflow; question and editor remain usable |
| Narrow Windows window | Settings/choices reflow; Tab focus is visible |
| Android phone, keyboard open | Editor and grading controls can be reached; the draft survives reflow |
| Android tablet, portrait/landscape/split-screen | Wide diagrams and choices reflow without losing the question |
| Large system text, both languages/themes | Labels wrap without clipping; stacked fractions remain readable |
| Narrator/TalkBack | Read question, fractions, visible data and feedback; hidden answers stay unknown |

The automated checks do not establish physical tablet/phone visual correctness,
keyboard inset behavior, actual OS DPI changes or full screen-reader interaction.

Quiz topic and subtype pickers now open the shared `IllustratedQuizPicker` modal.
The same chooser is used for the AI question-bank topic, operation and unknown
role. The collapsed field contains a small mathematical illustration, its name
and a chevron, keeping the configuration panel compact. Native Picker values and
selection events remain the backing model; no generation or grading route changes.

The geometry calculator's “2. Chọn hình” field uses the same illustrated chooser.
Its options come from GeometryFormulaCatalog: 11 plane shapes or 5 solids according
to the selected category, keyed by Calculator.Geometry.<shape Id>. The backing
Picker retains an explicit BindingContext after leaving the visual tree, and its
Items use ItemDisplayBinding for translated model names. Choosing a card continues
to update the formula preview, diagram and dimension fields through the existing
selection event. Shape descriptions are localized in QuizContent language packs;
thumbnails distinguish triangle/trapezoid variants and the five solid shapes.

The modal is centered, capped at 1100 by 780 logical units, with a search field
and one internally scrolling CollectionView on wide displays. Compact displays
can use their full available height. Its cards use 1–3 columns according
to available width and system text scale. Phones retain a single list when rotated;
tablets and desktop windows reflow according to their actual available width.
Narrow windows and phones show an illustration on the left and text on the right.
Search matches
localized titles and descriptions, including Vietnamese without accents, while
preserving the original selection indexes. Filtering retains the original panel
footprint so the search field does not jump as results change or become empty.

Single-column choices use LinearItemsLayout and MeasureAllItems, automatic card
height, a 44-unit thumbnail and fully wrapped titles/descriptions. Wide grids use
uniform 152-unit rows (scaled with system text), 54-unit thumbnails and previews
of up to three title/description lines. Full text remains available to screen
readers in both modes. The check mark occupies only the title row, preserving
description width. Card minimum height is 96 units; both Close controls have a
minimum 48 × 48 target. The same input-transparent decorative overlay keeps the
whole card clickable for mouse and touch.

The page uses SafeAreaEdges.All; layout bounds come from the inset content Grid,
accounting for system bars, cutouts and the soft keyboard. Below 400 units of
usable height, choices use a list. Below 260, Close shares the search row and the
visual heading is hidden (the accessible page Title remains). Submitting search
attempts to hide the soft keyboard. Android does not focus search on arrival;
Windows desktop search receives keyboard focus. Tab/Enter/Space use native button
behavior and Escape closes the Windows modal. Resizing and rotation recompute
the layout without changing the selected catalogue index.

Layout regression checks include logical widths 280–3840, usable heights
160–2160, font scales 1–3, Android/Windows scrollbar gutters, portrait tablets,
laptops, rotated phones and keyboard-reduced viewports. These are layout and
control checks, not a substitute for device interaction and screen-reader tests.

On Windows, the list reserves a 24-unit gutter inside its native ItemsPresenter
for the overlay scrollbar. Card columns share the remaining width evenly, and
the responsive column calculation excludes this gutter. The scrollbar remains
visible and draggable without covering card borders or keyboard focus outlines.

The current selection has a stronger border and a check mark. Cards are native
buttons with a visible focus border and localized accessible name/description;
decorative drawings are excluded from screen readers. Selection, Close, Windows
Escape and Android Back dismiss the modal; focus returns to the original field.
Changing app language dismisses an open chooser so its next opening uses the new
language. Illustrations follow the current theme and accent through resources.

Collapsed illustrated fields match the hardware-page pickers: InputBackgroundColor,
BorderBrush, a 10-unit corner radius, 15-unit bold text and a small vector chevron.
The 32-unit thumbnail and inset padding fit a 48-unit minimum on Windows and
56 on Android, with wrapping for larger text. The input button uses an opaque
theme surface, so hover shades that surface instead of filling it with the accent.
Composite picker buttons opt out of scale/fade press animation: the fill, caption,
thumbnail and chevron stay aligned with the frame throughout a click. Focus changes
the outer stroke color at constant thickness, rather than adding an inner outline.
Native hover/ripple and keyboard focus feedback remain enabled.

Chooser focus callbacks only update outlines while their native handlers have a
PlatformView. MAUI can raise Unfocused after clearing PlatformView during modal
teardown; changing Button.BorderWidth at that point would crash its stroke mapper.
Loaded callbacks restore the outline when controls are recycled or reconnected.
The caption/illustration overlay is wrapped in an input-transparent ContentView.
On Windows this disables native hit testing for the entire decorative subtree,
including nested layouts and spaces between labels. The underlying Button owns
hover, clicks and keyboard activation across the full field/card surface.

Presentation IDs are in `QuizContent/catalogues.json`, list `QuizChoices`.
Skill selectors use separate mathematical thumbnails for each skill (table, bars,
pie, pictograph, calendar, balances, fraction transformations, and so on).
The shared renderer is `Graphics/Quizzes/QuizChoiceDrawable*.cs`; IDs may be reused
across different selectors for the same skill or shape. Do not reuse a category
thumbnail for different skills within one selector. Catalogue tests check this
rule, and `UnitTest/QuizIllustrationTests` compares actual canvas commands and
checks drawing at field, card and enlarged sizes. Its optional `--preview <file>`
argument exports an HTML gallery from the same drawing commands for visual review.
Descriptions and chooser UI text are in each language pack. See
[authoring instructions](QUIZ_CONTENT_AUTHORING.md#9-bảng-chọn-dạng-bài-có-minh-họa).
Automated checks cover catalogue completeness, both language packs and filtered
selection routing. Still check actual keyboard navigation, Narrator/TalkBack,
large text and narrow/tablet layouts on devices before declaring visual coverage.
