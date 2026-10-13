# Components

These building blocks appear in every medium. They are taken from the live website and define role, hierarchy, and appearance. Each interpretation builds its own implementation.

## Actions

| Level | Appearance | Use |
| --- | --- | --- |
| **Primary** | Ebony fill and 1px Ebony border, Off-white label, 4 radius, at least 48 tall, 14 × 22 padding. The label is followed by an arrow at 20px, weight 400. Hover changes the fill to Dark Walnut and nudges the arrow 2px in its direction. | One per surface, for example `RENT FOTOHAVN ↓` |
| **Text link** | Uppercase Action or Meta label with a 1px underline offset by 7, followed by an arrow. The underline is Hairline at rest and the text color on hover. At least 44 tall. | Secondary paths, for example `EXPLORE THE EXPERIENCE ↓` or `ASK ABOUT YOUR DATE ↗` |
| **Secondary (bordered)** | Transparent fill and a 1px border in the current text color. | Rare. Paired choices, such as consent "Allow / Continue without". |

- On dark surfaces, the primary action inverts to an Off-white fill with an Ebony label.
- In static media (posters and end cards), render the action as a typographic line with its arrow, for example `ASK ABOUT YOUR DATE ↗ @FOTOHAVN.PH`. Do not draw a fake button.

## Eyebrow

An uppercase sans label that introduces a heading, in Soft Brown on light and Antique Metal on dark, for example `THE FOTOHAVN EXPERIENCE`, `ON THE OTHER SIDE`, or `BRING EVERYONE TOGETHER`. Over photography, use the derived secondary ink (see [color.md](color.md#derived-inks-and-tints)).

## Statement heading

The signature component: eyebrow, then a sentence-case serif heading with an italic turn, then one or two Supporting lines, then an optional action. It is centered for the hero over photography and for a single-subject moment such as the guest album ("Look at *you.*"). Everywhere else it is left-aligned.

## Path

A pair of offer articles separated by a Hairline rule. Each has an eyebrow (`MAKE A LITTLE DETOUR`), a serif heading ("Find the booth."), Supporting copy, and a text link with ↗. Use it whenever FOTOHAVN presents more than one way in.

## Print object

A photograph or Photo Strip shown as a physical object. It uses an Off-white or Cream Paper border, may rotate up to about 12° when scattered on a board, may be pinned with a silver clip, and takes the `print-contact` shadow when resting or the `print` shadow when lifted. Turning a print over reveals a short note on its back. A board of prints may carry a few small keepsake objects, such as a fabric bow, a ticket stub, or a plush charm, photographed as real objects and used sparingly.

## Option list

Use an option list for choosing one of a set, such as templates or looks. Each row has a small thumbnail, a serif Subheading title, a Supporting description, a count or meta line, and a circular radio. The selected row gets a Warm Ivory tint, a 2px Muted Brass left rule, and a filled brass radio. Rows are separated by Hairline rules.

## Step progress

A numbered row of steps (`01 Template`, `02 Photographs`, …). Numbers use the Meta role. The current step is Ebony with a 2px Ebony underline, and the others are Soft Brown.

## Navigation and footer

- **Navigation:** the wordmark on the left; uppercase Meta links; one primary action on the right (`RENT FOTOHAVN`). On mobile, a `MENU` text button with two lines.
- **Footer:** Ebony surface. The brand line appears as a small uppercase label, platform links with ↗, and a **full-width oversized wordmark** in Off-white, followed by `© YEAR FOTOHAVN` and `BACK TO TOP ↑`.

## Media frame

Photography sits flush or in a 2-radius frame on Cream Paper. Captions use the Supporting or Meta role and align to the image edge. Nothing overlays the image except a statement heading in genuine negative space.
