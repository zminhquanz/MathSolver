# Optional Icons8 artwork

This application uses a selective set from **Fluent Systems Regular**. `icons8.json` at the repository root records the exact Icons8 IDs and glyph assignments. The original math-topic icons and clearer original actions remain in use.

## License boundary

Icons8 artwork is licensed separately from this repository's GPLv3 code. The SVG files, generated font and generated path-data source are local build inputs and are ignored by Git. Do not commit them or offer them as a standalone asset/font download. A paid subscription/API entitlement does not make the artwork GPL or grant unrestricted asset redistribution.

Official terms: [Icons8 license](https://icons8.com/license), [Universal Multimedia License](https://intercom.help/icons8-7fb7577e8170/en/articles/5534926-universal-multimedia-license-agreement-for-icons8). Check the applicable subscription and terms before publishing an application that embeds these assets. No account credentials are stored here.

## Local build

1. Obtain the selected SVGs using your own authorized Icons8 account, preserving the filenames in `icons8.json`.
2. Run `node tools/branding/generate-icons8-actions.mjs` from the repository root. It validates the fill paths and generates the local font, header paths and SHA-256 provenance record.
3. Build normally. The licensed pack is enabled only when both generated inputs exist. `-p:UseLicensedIcons=false` explicitly builds with the original artwork.

A public clone without the licensed inputs uses the existing original icons and emoji fallbacks. It requires no Icons8 account, network request, JavaScript runtime or font generation to run. Generated artifacts must not be individually extracted/repackaged as a font product.
