# RtfDomParser

C# library for parsing RTF documents into a DOM tree and writing RTF back out. Originally
`DCSoft.RTF` / `XDesigner.RTF` by yuan yong fu; now maintained here targeting .NET 7.

## Solution layout

- `Source\RtfDomParser.sln` — the solution (open/build this, not individual csproj files, when
  in doubt, since the demo project depends on the library).
- `Source\RtfDomParser` — the library (`net7.0`). All types live in a single `RtfDomParser`
  namespace (there is no `DCSoft.RTF` namespace despite the name/history — old file header
  comments referencing DCSoft are leftover and not authoritative).
- `Source\RtfDomParser.Tests` — NUnit test project (`net7.0-windows`, uses WinForms).
- `Source\RtfDomParser.WinFormsDemo` — WinForms sample app referencing the library.

## Rules
- **Never** commit or push changes in git. User should **always** review the changes and commit manually.

## Build

```powershell
dotnet build Source\RtfDomParser.sln
```

CI (GitHub Actions, see `.github\workflows\main.yml`) builds, packs, and publishes only the
`RtfDomParser` library project (not the tests or demo project) to GitHub Packages on every push
to `master`/`develop`; `develop` pushes get a `-beta` version suffix and `Debug` configuration.
There is no separate lint step.

## Tests

Tests are NUnit (`RtfDomParser.Tests`) but are **interactive, manual smoke tests, not
automated assertions**:
- `TestWriteFile` writes `a.rtf` and pops a `MessageBox` telling you to open it manually.
- `TestClipboard` copies RTF to the Windows clipboard and pops a `MessageBox` telling you to
  paste into MS Word manually.

Because of the `MessageBox.Show` calls, running them via `dotnet test` will block waiting for
UI dismissal — expect this, don't treat it as a hang. There are no headless/CI-safe unit tests
in this repo currently; when adding new automated tests, prefer real assertions over the
existing MessageBox-based pattern. Run a single test with:

```powershell
dotnet test Source\RtfDomParser.Tests\RtfDomParser.Tests.csproj --filter "Name=TestWriteFile"
```

## Architecture

The library implements a classic **tokenize → raw node tree → DOM tree** pipeline for parsing,
and the reverse for writing:

1. **`RTFReader`/`RTFToken`** — low-level tokenizer that turns raw RTF bytes into a stream of
   tokens (control words, groups, text), handling encodings via `Defaults`/font/code page info.
2. **`RTFNode` / `RTFNodeGroup` / `RTFNodeList`** — a generic tree of raw RTF nodes/groups
   built from tokens (`RTFNode(RTFToken token)` constructor maps token type to `RTFNodeType`).
   `RTFRawDocument` holds this raw tree.
3. **`RTFDomDocument`** (in `RTFDomDocument.cs`, a large `partial class` deriving from
   `RTFDomElement`) — the public DOM entry point. `Load(string fileName)` /
   `Load(Stream)` / `Load(TextReader)` / `LoadRTFText(string)` parse RTF into a structured DOM
   of `RTFDomElement` subclasses: `RTFDomParagraph`, `RTFDomTable`/`RTFDomTableRow`/
   `RTFDomTableCell`, `RTFDomText`, `RTFDomImage`, `RTFDomShape`/`RTFDomShapeGroup`,
   `RTFDomField`, `RTFDomBookmark`, `RTFDomLineBreak`/`RTFDomPageBreak`, etc. `RTFDomElementList`
   / `RTFDomElementContainer` provide the child-collection plumbing shared by container elements.
4. **`RTFDocumentWriter`** and **`RTFWriter`** — the reverse direction: `RTFWriter` is a
   low-level RTF token writer (`WriteStartGroup`/`WriteKeyword`/`WriteText`/`WriteEndGroup`),
   and `RTFDocumentWriter` walks a `RTFDomDocument` and serializes it back to RTF using
   `RTFWriter`.
5. Supporting tables (`RTFFontTable`, `RTFColorTable`, `RTFListTable`, `RTFListOverrideTable`)
   are attached to the document and referenced by index from DOM nodes (fonts/colors/list
   formatting are not inlined per-node).

`Defaults` centralizes static configuration (default font name, encodings) and must have its
encodings loaded before parsing — `RTFDomDocument`'s static constructor calls
`Defaults.LoadEncodings()` automatically, so callers normally don't need to do this themselves,
but tests explicitly set `Defaults.FontName` in `[SetUp]`.

## Conventions

- Image/shape/graphics rendering code (`RTFUtil`, parts of `RTFDocumentWriter`,
  `RTFDomDocument`) uses `System.Drawing`/GDI+ APIs that only work on Windows (`CA1416`
  warnings are expected and currently unsuppressed at the call sites, only suppressed globally
  in the test project). Don't "fix" these warnings by removing Windows-only functionality.
- Public API surface is large and mostly property-based (get/set) mirroring RTF concepts
  1:1 (e.g. `RTFAlignment`, `RTFBorderStyle`, `RTFVerticalAlignment` enums map directly to RTF
  keywords) — when adding RTF feature support, follow the existing pattern of an enum/type in
  its own file plus wiring in both `RTFDomDocument`/element classes (parsing) and
  `RTFDocumentWriter` (writing).
