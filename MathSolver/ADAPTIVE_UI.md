# Adaptive practice and solver layout

Practice settings stay open on entering a fresh session. Start practice, focusing
the essay editor, or submitting an answer collapses them into a bilingual summary.
Change settings opens the same controls without regenerating the current question
or clearing work. Existing selection handlers retain their previous behavior when
the user actually changes a selection. Leaving practice resets the collapsed state;
opening settings, diagrams or the AI question bank preserves it.

The AI question-bank action and running-job progress stay in the summary, outside
the collapsible settings. Both Windows and Android can select/import/download a
GGUF model and start a background question-generation job, then return to practice
without waiting for inference or losing their draft. On narrow layouts the two
summary actions share a row below the summary text.

The current optional bank supports basic arithmetic word problems; it does not
restore the former AI practice tab or LLM accuracy benchmark. There is no Windows,
AVX2 or installed-RAM visibility gate on this bank. Android packages the ARM64 CPU
backend. Practice reads committed SQLite templates or immediately uses fresh C#
questions when no matching template exists; it never awaits model generation.
Generation keeps the existing worker budget and releases model weights after each
job. On Android, Home/locking the screen stops generation; navigating inside the
app does not. This is an in-app background task, not a foreground service.

Ordinary solver reading/input pages use a centered viewport-bound layout capped
at 1320 logical units. Geometry, quadratic plots and the practice question card
can use 1440. Practice configuration caps at 1120 and the essay area at 960.
These are maximum widths, not minimum device requirements.

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
