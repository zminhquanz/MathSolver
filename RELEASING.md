# Publishing a Math Solver Release

This guide describes the recommended manual release process for GitHub.

## Recommended Version

For the first public preview:

```text
v0.1.0-beta.1
```

Use `v1.0.0` only when you consider the application stable for general use.

## 1. Prepare the Application

Before publishing:

- Update the application display version.
- Build and test the Windows package.
- Build and test the Android package.
- Confirm that README.md and LICENSE are current.
- Remove old `bin` and `obj` folders before the final clean build.
- Test the packages on a clean device or user account when possible.

## 2. Name the Release Files

Recommended names:

```text
MathSolver-v0.1.0-beta.1-win-x64.zip
MathSolver-v0.1.0-beta.1-android.apk
SHA256SUMS.txt
```

Use predictable names because GitHub supports stable links to assets in the latest release.

## 3. Generate SHA-256 Checksums

### Windows PowerShell

```powershell
Get-FileHash .\MathSolver-v0.1.0-beta.1-win-x64.zip -Algorithm SHA256
Get-FileHash .\MathSolver-v0.1.0-beta.1-android.apk -Algorithm SHA256
```

Copy the hashes into `SHA256SUMS.txt`.

## 4. Create the Release in GitHub

1. Open the repository on GitHub.
2. Select **Releases**.
3. Select **Draft a new release**.
4. Choose **Create new tag**.
5. Enter:

```text
v0.1.0-beta.1
```

6. Target the `main` branch.
7. Set the release title to:

```text
Math Solver v0.1.0 Beta 1
```

8. Paste and edit `RELEASE_NOTES_TEMPLATE.md`.
9. Upload the Windows ZIP, Android APK, and `SHA256SUMS.txt`.
10. Enable **Set as a pre-release** for beta versions.
11. Save as a draft first.
12. Download and test the uploaded assets.
13. Publish the release.

## 5. Generated Release Notes

The file `.github/release.yml` customizes GitHub's **Generate release notes** feature.

For the best results, label pull requests with labels such as:

```text
feature
enhancement
bug
ui
geometry
numerics
performance
documentation
maintenance
```

## 6. GitHub CLI Alternative

After installing and authenticating GitHub CLI:

```bash
gh release create v0.1.0-beta.1 \
  "MathSolver-v0.1.0-beta.1-win-x64.zip#Windows x64" \
  "MathSolver-v0.1.0-beta.1-android.apk#Android APK" \
  "SHA256SUMS.txt#SHA-256 checksums" \
  --title "Math Solver v0.1.0 Beta 1" \
  --notes-file RELEASE_NOTES_TEMPLATE.md \
  --prerelease \
  --target main
```

To use GitHub-generated notes instead:

```bash
gh release create v0.1.0-beta.1 \
  ./dist/* \
  --generate-notes \
  --prerelease \
  --target main
```

## 7. Stable Download Links

Replace `OWNER` and `REPOSITORY`:

```text
https://github.com/OWNER/REPOSITORY/releases/latest
```

Direct download of an asset from the latest release:

```text
https://github.com/OWNER/REPOSITORY/releases/latest/download/MathSolver-win-x64.zip
```

A stable direct-download link works best when the asset name remains unchanged across releases. If version numbers are included in filenames, link to the release page instead.
