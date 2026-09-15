# FOTOHVN at Evia Mall — Reel comparison

Two deliberately different vertical Reel directions live in one Remotion folder. Neither direction is selected.

## Compositions

- `Evia-Packed-Today` — 450 frames, 15 seconds, 30 fps, 1080×1920. Rhythmic event energy authored on a 120 BPM / 15-frame grid, with six-frame fades connecting each scene.
- `Evia-Little-Room` — 540 frames, 18 seconds, 30 fps, 1080×1920. Warm editorial pacing authored on a 100 BPM / 18-frame grid, with nine-frame restrained fades while every scene still starts on-grid.

Start the comparison in Studio:

```console
npx remotion studio --no-open
```

## Media handling

The four rotated 4K HEVC phone clips did not seek reliably in Chromium. The originals remain untouched. Silent 1080×1920, 30 fps H.264 proxies are in `public/generated/proxies/` and are the sources used by the compositions. The smaller H.264 booth clip is used directly with `volume={0}`.

All video layers are explicitly muted. No guide or final track is embedded. `GuideAudio` is an intentionally empty, clearly labeled slot for a cleared temporary guide track if one is supplied later.

## Assets used

`Evia-Packed-Today` uses video sources `1000018850`, `1000018860`, and `1000018872`; location photos `1000018842`, `1000018861`, and `1000018865`; and guest portraits `1000018875`, `1000018876`, `1000018877`, `1000018879`, `1000018881`, and `1000018882`.

`Evia-Little-Room` uses video sources `1000018855`, `1000018860`, and `1000018872`; location photos `1000018842` and `1000018870`; and guest portraits `1000018875`, `1000018876`, `1000018877`, `1000018879`, and `1000018882`.

No standard price, physical price display, or promotion is shown. The story now stays on the event, the booth reveal, the enclosed experience, real guests, printed keepsakes, and the final visit CTA. The second scene is footage-only, and the video moments have been lengthened to avoid abrupt cuts.

The current typography and framing remain a temporary visual shell. Three independent ImageGen directions were created for human selection before the next design implementation pass.

## Platform-audio direction

For `Evia-Packed-Today`, search TikTok Commercial Sounds or Instagram's business-safe library for: `dynamic pop`, `weekend energy`, `bright funk`, `upbeat electronic`, or `feel good event` around 118–125 BPM.

For `Evia-Little-Room`, search for: `warm indie pop`, `nostalgic groove`, `soft disco`, `feel good soul`, or `photobooth memories` around 96–104 BPM.

No MP4 masters have been rendered yet. Review both compositions in Studio before exporting.
