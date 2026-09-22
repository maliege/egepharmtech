<!-- Thank you. Please keep the checklist; it mirrors CONTRIBUTING.md. / Teşekkürler; liste CONTRIBUTING.md ile aynıdır. -->

**What / Ne değişti**

**Why / Neden**

**Checklist / Kontrol listesi**
- [ ] Calculations live in `PTCalc.Core/Core/<Module>/` (namespace `PTCalc.Core.<Module>`), no ASP.NET/Blazor dependency
- [ ] User-facing core texts use `CoreText.T(...)`, English added to `CoreText.en.tsv`
- [ ] UI texts use `L["..."]`, English added to `SharedResource.en.resx`
- [ ] Tests added or updated; `dotnet test PTCalc.Core.Tests` passes locally
- [ ] Number formats checked for tr-TR (comma) and en-US (dot)
- [ ] Comparisons with other software are worded as comparisons, sources cited with DOI
