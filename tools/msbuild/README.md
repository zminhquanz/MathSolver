# PDFsharp engine without digital signing

The app and PDF tests import `PdfSharpCore.props` instead of referencing the
entire PDFsharp NuGet package. The upstream 6.2.4 package bundles
`PdfSharp.Cryptography.dll`, which introduces `System.Security.Cryptography.Pkcs`
8.0.1. Math Solver does not sign PDFs or use that assembly.

The official package is downloaded at an exact version with `PackageDownload`.
Only `PdfSharp.dll`, `PdfSharp.Shared.dll` and `PdfSharp.System.dll` are referenced
and copied to the app. Their external logging dependency is explicitly declared.
There are no private copies of upstream binaries or edits to the NuGet cache.
The build rejects reintroduced signing/PKCS references. PDF creation, font
embedding, drawing and PDF parsing remain available.

`PackageDownload` performs NuGet package signature/source checks but does not
resolve dependencies or appear in `dotnet list package`. The PDFsharp version is
therefore recorded explicitly in this props file; review the three assemblies'
dependencies and upstream security notices when upgrading it. NuGet auditing for
normal package references remains enabled. Do not add `NoWarn` or disable audit
to remove vulnerability messages.

References:

- [PDFsharp: digital signer is in a separate assembly](https://docs.pdfsharp.net/PDFsharp/Topics/PDF-Features/Signatures.html)
- [NuGet PackageDownload semantics](https://learn.microsoft.com/en-us/nuget/consume-packages/packagedownload-functionality)

Validation: restore both importing projects, confirm PKCS is absent from their
resolved package graphs and build outputs, run `UnitTest/QuizPdfTests`, and build
the Windows/Android app targets. Existing build directories may still contain
old dependency files; validate a clean output directory or rebuild before shipping.
