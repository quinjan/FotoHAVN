# Typography

FOTOHAVN pairs an expressive editorial serif with a clean, modern sans. The serif carries emotion through sentence-case statements that end in an italic turn. The sans does the work: eyebrows, actions, and supporting copy.

| Family | Use | Weights | Fallbacks |
| --- | --- | --- | --- |
| **Cormorant Garamond** | Headings, statements, option titles, wordmark | 400 roman and italic (headings); 500 (wordmark only) | Times New Roman, Georgia, serif |
| **Manrope** | Eyebrows, actions, navigation, body, supporting copy | 400 (reading); 500–600 (eyebrows, actions, meta) | Segoe UI, Arial, sans-serif |

Both families are available from Google Fonts under the SIL Open Font License 1.1, which permits use in print, video, apps, and embedded documents. Install them, or embed them in documents that leave the team. Fallbacks must still produce a readable result.

## Type roles

These are the live website's values at 1×. Ranges show where the website varies a role fluidly. Other media scale by role (see [mediums.md](mediums.md#type-scaling)). Exact values are in [tokens/tokens.json](tokens/tokens.json).

| Role | Size | Line height | Tracking | Family | Weight | Case |
| --- | ---: | ---: | ---: | --- | ---: | --- |
| Eyebrow | 12 (10–12) | 1.4 | 0.18em (to 0.24em) | Sans | 600 | Uppercase |
| Meta | 11 | 1.4 | 0.08em | Sans | 600 | Uppercase |
| Action | 12 | 1.4 | 0.1em | Sans | 600 | Uppercase |
| Supporting | 14 (13–14) | 1.8 | 0 | Sans | 400 | Sentence |
| Body | 16 | 1.65 | 0 | Sans | 400 | Sentence |
| Body large | 20 (16–20) | 1.6 | 0 | Sans | 400 | Sentence |
| Subheading | 28 | 1.2 | −0.015em | Serif | 400 | Sentence |
| Heading small | 44 | 1.1 | −0.035em | Serif | 400 | Sentence |
| Heading | 56 | 1.0 | −0.035em | Serif | 400 | Sentence |
| Heading large | 88 (64–136) | 0.97–1.08 | −0.045em | Serif | 400 | Sentence |
| Display | 108 (52–118) | 1.0 | −0.047em | Serif | 400 | Sentence |
| Emphasis (italic turn) | Inherits | 1.12–1.18 | Inherits | Serif italic | 400 | Sentence |

## The italic turn

The signature typographic move. A serif heading is set in sentence case, and its **closing phrase is set in italic**:

> Photographs, *developed differently.*
> Small room. *Whole other world.*
> Keep the moment *close.*
> Let's make something *worth keeping.*

- Use one italic turn per heading, always at the end. It can be one word or a short phrase.
- The italic may break onto its own line. When it sits alone on its own line, it may drop to 0.86em of the heading size.
- Use the same weight, 400, for roman and italic. Never bold the italic.
- Never set a whole heading, body copy, or a sans label in italic.

## Rules

- Headings are sentence case and weight 400. Larger serif sizes get tighter tracking, from −0.035em down to −0.047em.
- Eyebrows sit above headings in Soft Brown, 18–28 units above.
- Supporting copy under a heading is short, in Soft Brown, and set at 13–16px with generous 1.65–1.8 leading. Keep it to 30–42 characters per line.
- Reading prose stays within 45–65 characters per line.
- Never use a weight above 600. Never use faux bold or faux italic.
- Centered type is reserved for the hero statement over photography and single-subject moments such as the guest album heading. Section content is left-aligned.
- Never use script, condensed display, outlined, distressed, typewriter, or novelty fonts.
- Use the wordmark master in non-web media. See [logo.md](logo.md).

## Hierarchy pattern

1. **Eyebrow**: `THE FOTOHAVN EXPERIENCE`.
2. **Heading**: serif, sentence case, with the italic turn: "A little privacy. A little *possibility.*"
3. **Supporting**: one or two short sans lines in Soft Brown.
4. **Action**: uppercase sans with an arrow glyph, either as a primary button or a text link.
