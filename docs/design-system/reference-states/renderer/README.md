# Reference-state renderer

This deterministic, repository-native renderer produces the visual targets registered by `../registry.json`. It is a review instrument, not production FotoHAVN code and not a second design authority.

- Contract anchor: `design-v1.0.0` / `1a20bc6f46f8d724683f8c4b47c359379fc7371b`
- Visual inputs: the approved contract, audit identity baseline, and the issue 20/28 Camera and Photo Strip evidence already stored in this repository.
- Query shape: `?surface=<surface>&state=<state>&viewport=<width>x<height>`.
- Output: the exact viewport, with a small target/provenance label and a matching YAML annotation sidecar.

The renderer intentionally has no interaction or product persistence. Its only job is to make the approved contract reviewable at the required states and sizes. Production WinUI must later be captured independently and compared against these targets.
