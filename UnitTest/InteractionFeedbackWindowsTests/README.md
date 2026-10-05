# Native Windows feedback regression

Run on Windows: `dotnet run --project UnitTest/InteractionFeedbackWindowsTests`.
The harness links the production feedback implementation and tests real WinUI
Button/ImageButton resources on the UI thread, including repeated refresh and
the three animation targets. It opens no window and does not use app data.
Controls are created by the real MAUI handlers, so existing native resource keys
are present just as they are during app startup. The tests also cover foreground,
stroke, disabled state and detach/reattach.
Initial border color/thickness are checked before any click in light and dark
themes, including width changes and deliberately borderless controls.
It also checks the keyboard focus outline, loads the production solver styles,
measures long wrapped choices with 16/24/32-point text in both themes, and checks
viewport width bindings at 360/800/1920 logical units. This does not change the
user's Windows DPI or accessibility settings.
The result is written to `result.txt` beside the executable, or to the path
passed as the first argument. Exit code 0 means success.

This catches native resource insertion errors that the portable palette tests
and a successful MAUI build cannot detect. Actual pointer interaction and Android
ripples still need manual UI verification.

To reproduce the previous resource insertion failure, pass a report path and
`--reproduce-previous` after `--`. This diagnostic intentionally returns exit
code 1 with the native COMException when the old pattern fails.
