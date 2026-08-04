# Operator Event UI prototype

Three variants of the operator-facing Saved Events and Event setup flow, switchable with `?variant=A`, `?variant=B`, or `?variant=C` on the prototype root.

This is throwaway design-validation code for FotoHAVN issue 19. It uses in-memory state only and preserves the lifecycle and setup decisions validated in issue 18.

Run it with one command:

```powershell
npm run dev
```

Variant A is the selected Attio-inspired visual reference. Variant B promotes the live preview into a wider workspace. Variant C places identity and fixed hardware in a setup rail. The bottom switcher and left/right arrow keys move between variants while keeping the URL stable.

`Year-End Party` is the deterministic incomplete-deletion fixture. Its quarantined recovery state survives reloads through minimal prototype recovery metadata. Open with `?resetDeletion=1` to clear that fixture and return to the clean Saved Events state.
