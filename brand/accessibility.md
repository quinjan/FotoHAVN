# Accessibility

Legibility and calm are part of the brand. Every interpretation meets this baseline. An interpretation may raise it but never lower it.

## Contrast

- Normal text: at least **4.5:1**. Large text (24px or larger, or 18.66px bold) and essential non-text elements: at least **3:1**. The approved pairs are in [color.md](color.md#contrast-pairs).
- Never use Muted Brass for small text on light surfaces. Use Antique Metal only on dark surfaces. Use Ebony or Dark Walnut for secondary text on Cream Paper.
- Text on photography must pass these ratios against the actual area behind it. Use the minimum directional overlay needed.

## Focus and input

- Every interactive element shows a visible focus indicator: a 2-unit outline at a 5-unit offset, in Ebony on light surfaces or Off-white on dark ones.
- Touch and pointer targets are at least 44 × 44.
- Keyboard order follows the visual reading order. Tabs and selectors support arrow keys.
- Never require hover, drag, precision tapping, or a time limit to complete a task.

## Meaning

- Never communicate state with color alone. Pair the brass mark with text, weight, an ARIA state, or a position change.
- Use semantic structure: headings in order, landmarks, real buttons and links, lists, and tabs.
- Action labels describe the destination or result, for example `ASK ABOUT YOUR DATE ↗` rather than "Click here".

## Text

- Reading text is at least 16px on web and app screens, 9pt in print, 28px on 1920 × 1080 slides, and 36px in vertical video at 1080 wide. See [mediums.md](mediums.md#type-scaling).
- Short Supporting copy under a heading may go down to 13px on screen, with 1.8 leading, and must still meet 4.5:1 contrast. Eyebrow and Meta labels may be 10–12px uppercase. Never use those sizes for paragraphs a visitor must read to complete a task.
- Prose stays within a 45–65 character measure. Avoid long centered paragraphs.
- Uppercase is for short lines only.

## Imagery

- Give each meaningful photograph concise, purposeful alt text. Give decorative textures empty alt text.
- In video, burn in captions for any spoken or essential audio, and keep them inside the safe area.

## Motion and flashing

- Respect the reduced-motion preference in every interactive medium (see [motion.md](motion.md#reduced-motion)).
- Never flash more than three times per second in any medium. A capture may use one simulated exposure flash.

## Verification

Each interpretation names how it verifies this baseline. Examples are rendered-browser checks at the required widths, the desktop UI verification host, a proof review for print, or a frame review for video. The baseline is not a formal certification.
