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
