# Embedded fonts

The compositor draws the `FOTOHAVN` signature and the other lettering with these static TrueType files, loaded from embedded resources into SkiaSharp. Nothing is ever read from the system font manager.

Both fonts are licensed under the SIL Open Font License 1.1; the licence texts sit next to the fonts (`OFL-CormorantGaramond.txt`, `OFL-Manrope.txt`). They are the same releases the UI bundles as WOFF2 from the exact-pinned `@fontsource` packages (5.3.0), converted by Fontsource to TrueType for the Latin subset, which covers `FOTOHAVN` and the Latin signature copy.

| File | Family and weight | Source URL | Version | SHA-256 | Licence |
| --- | --- | --- | --- | --- | --- |
| `CormorantGaramond-Medium.ttf` | Cormorant Garamond 500 (Medium), latin | https://cdn.jsdelivr.net/fontsource/fonts/cormorant-garamond@5.3.0/latin-500-normal.ttf | `@fontsource/cormorant-garamond` 5.3.0 | `f372d662a48554b94189673e28eb5a0cbd75bb0b4662df3a26d60efb46dd7073` | OFL 1.1, Copyright 2015 The Cormorant Project Authors (github.com/CatharsisFonts/Cormorant) |
| `Manrope-Regular.ttf` | Manrope 400 (Regular), latin | https://cdn.jsdelivr.net/fontsource/fonts/manrope@5.3.0/latin-400-normal.ttf | `@fontsource/manrope` 5.3.0 | `4eefc9018b1a6775d54c7c015f1067eaa1272628bc348b220d541c7e4a8ab38a` | OFL 1.1, Copyright 2019 The Manrope Project Authors (github.com/sharanda/manrope) |

Downloaded 2026-10-14. A test hashes both embedded files against the SHA-256 values above, so a changed file fails the build until this record is updated.
