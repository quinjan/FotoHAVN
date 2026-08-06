# Event identification in consequential flows

> PROTOTYPE — throwaway code. Do not promote directly to production.

Question: **Which concrete presentation of the approved Event identification model works best across saved-Event cards and consequential confirmations at supported booth sizes and scaling levels?**

The approved saved-card identity is fixed in all variants: Event name, `EVENT ID`, grouped final eight UUID characters, then saved recency. Three full-ID presentations are switchable with `?variant=A|B|C`:

- **A — Identity panel:** a compact bordered identity block.
- **B — Sentence-led:** a plain-language compact-ID sentence followed by the full identifier.
- **C — Verification rows:** explicit Event and Full Event ID label/value rows.

The state control covers Edit, Start confirmation, Starting, Could not start, permanent-delete confirmation, Deleting, Deletion incomplete/Retry, and Deletion complete. The view control covers 1280 × 720, 1024 × 768, 125%, 150%, and a 200% zoom-equivalent stress case.

Run from this directory:

```powershell
.\run.ps1
```

Then open <http://127.0.0.1:4181/event-identification-prototype/?variant=A&flow=start&viewport=canonical>.

This prototype records no data and makes no production mutations. Issue #55 stays open until the operator selects or combines a presentation.
