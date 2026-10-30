# Math navigation vectors

Twelve monochrome 24×24 SVG originals cover fifteen Solve/Formula entries.
`tools/branding/generate-navigation-icons.mjs` is their shared geometry source.
Run `node tools/branding/generate-navigation-icons.mjs` from the repository root
after changing geometry. No external packages are required.

The same filled outlines are compiled into `Resources/Fonts/MathNavigationIcons.ttf`.
MAUI `FontImageSource` renders this small vector font at 24 units, binding Color
directly to Button.TextColor. This keeps exact accent colors on Android and avoids
platform image-tint handlers or a separate raster asset for every theme color.
The font is registered for icons only; it is not offered as a text font in Settings.

On Windows, `NavigationFontImageSourceService` renders these icons on a fixed
24×24 logical canvas at the requesting window's DPI. The stock MAUI font-image
service forces 96 DPI, which otherwise causes blur at 150/200% display scaling.
The dedicated source type keeps that fix isolated from other app icons; Android
continues to use MAUI's native font-image renderer.

`MathNavigationCatalog` maps translation keys to glyphs. `MathNavigationIcon.Key`
attaches the same mapping to native tabs, chooser entries and compact selectors.
Keep labels in the localization resources free of emoji or decorative symbols.
Known prefixes from older imported language packs are removed only for these keys.
Page headings and mathematical formulas are independent of these navigation icons.
