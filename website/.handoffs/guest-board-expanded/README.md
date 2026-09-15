# Expanded guest board

The user requested smaller photographs and more assets mixing real repository pictures with board keepsakes. The board now contains 18 unique photographs (14 guest photographs and four complete published strips), plus six keepsake placements using the existing teddy, bunny, ribbon and ticket artwork.

Large desktop prints reduced from roughly 24–26% of board width to 15.5–18%; phone portraits reduced from 58–61% to 43–47%. Strips retain a 60px minimum width. Desktop uses three staggered groups and phones use a taller, naturally scrolling arrangement. Irregular rotations and overlaps are retained; hover/focus raises a print above its neighbours and keepsakes.

## Added real photographs

Source directory: `C:/Quinjan/Repos/FotoHAVN/marketing/reels/FOTOHAVN-evia-event/public/assets/Pictures/`.

| Board ID | Original | Runtime asset |
| --- | --- | --- |
| guest-sisters | 1000018877.jpg | /images/evia/sisters.webp (existing derivatives reused) |
| guest-trio | 1000018874.jpg | /images/guest-board/guest-trio.webp |
| guest-solo | 1000018875.jpg | /images/guest-board/guest-solo.webp |
| guest-duo | 1000018880.jpg | /images/guest-board/guest-duo.webp |
| guest-flowers | 1000018881.jpg | /images/guest-board/guest-flowers.webp |
| guest-three | 1000018882.jpg | /images/guest-board/guest-three.webp |
| guest-close | 0010dffb-cbe9-4499-b885-83a6c171c9d5.jpg | /images/guest-board/guest-close.webp |
| guest-together | 64b9e7d1-c76b-44e8-b699-b69e466680d2.jpg | /images/guest-board/guest-together.webp |

The existing repository contact sheet was inspected to select these images and ground their alt text/editorial descriptions. Full original photographs are preserved. `scripts/prepare-expanded-board.mjs` creates the seven new base WebPs and all seven responsive candidates for each, with orientation corrected and no enlargement or crop. The eighth image reuses existing prepared assets.

The two extra decorative placements reuse the existing ribbon and flower-ticket assets at different angles. No additional generated imagery, customer quotes or guest identities were invented. Every new photograph has an editorial note and opens in the existing viewer.

## Verification

See `../../design-qa.md`. Evidence in this directory includes desktop, tablet, three phone scroll positions and a new photo's editorial note. Previous QA is archived as `previous-design-qa.md`.

Board tests were updated for the enlarged collection and now also check that every image-loader candidate exists for every record. All eight tests, production build/TypeScript and focused lint passed. Existing unrelated dirty work remains preserved. No commit, push or deployment.
