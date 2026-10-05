# Interaction color checks

Run `dotnet run --project UnitTest/InteractionFeedbackTests -c Release`.
The console harness checks the production palette against independent WCAG
contrast measurements, including existing low-contrast themes, selected buttons,
correct/incorrect answer colors and transparent icon/menu backgrounds.

Native UI verification is separate: on Windows check enter/leave, press/release,
drag outside, disabling a hovered button, theme changes while hovering and repeated
navigation. On Android check taps, ripple masks, scrolling through settings rows,
disabled controls and enter/leave with a connected mouse. Verify that clicks/taps
still fire once and that selected/graded colors return after interaction.
Android 5.x retains native feedback where foreground ripple layers are unavailable.
