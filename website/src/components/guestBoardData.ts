export type GuestPhotograph = {
  id: string;
  image: `/${string}`;
  alt: string;
  title: string;
  note: string;
  kind: "portrait" | "strip";
  tilt: number;
  testimonial?: { quote: string; attribution: string };
};

// Editorial captions, approved as the interim content. Do not turn these into
// attributed customer speech. Only add testimonial records with approved quotes.
export const guestPhotographs: GuestPhotograph[] = [
  { id: "sepia-strip", image: "/images/guest-board/strip-sepia.webp", kind: "strip", tilt: -12,
    alt: "Four-frame sepia Photo Strip of a guest with a small white dog.", title: "A little company.",
    note: "A small companion, held close. Four sepia frames from the FOTOHAVN collection." },
  { id: "friends", image: "/images/evia/friends.webp", kind: "portrait", tilt: -7,
    alt: "Four friends holding their Photo Strips outside FOTOHAVN at Evia.", title: "The more, the merrier.",
    note: "Four friends and a handful of photographs. A little frame for all that personality, from our weekend at Evia." },
  { id: "classic-strip", image: "/images/guest-board/strip-classic.webp", kind: "strip", tilt: 11,
    alt: "Four-frame monochrome Photo Strip of a guest wearing glasses, making different expressions.", title: "A little personality.",
    note: "A different expression in every frame. An original Photo Strip from the FOTOHAVN collection." },
  { id: "couple", image: "/images/evia/couple.webp", kind: "portrait", tilt: 6,
    alt: "Two guests smiling together outside the cream-curtained booth.", title: "Just the two of you.",
    note: "Two guests, side by side, with their photographs in hand. A moment from the FOTOHAVN guest album at Evia." },
  { id: "monochrome-strip", image: "/images/guest-board/strip-monochrome.webp", kind: "strip", tilt: 13,
    alt: "Four black-and-white portraits of a guest in front of a patterned curtain.", title: "In black and white.",
    note: "Four portraits, a patterned curtain, and the small changes between one expression and the next." },
  { id: "family", image: "/images/evia/family.webp", kind: "portrait", tilt: 5,
    alt: "A group of guests with a child showing their photographs at Evia.", title: "Everyone’s in the picture.",
    note: "A group gathered close, showing their printed photographs. There is room for everyone in this little memory from Evia." },
  { id: "smiles", image: "/images/evia/smiles.webp", kind: "portrait", tilt: -9,
    alt: "Two smiling guests holding up their Photo Strips.", title: "One for you. One for me.",
    note: "Two smiles and photographs to take home. From a weekend of good company at FOTOHAVN, Evia." },
  { id: "naturale-strip", image: "/images/guest-board/strip-naturale.webp", kind: "strip", tilt: -13,
    alt: "A full-color, four-frame Photo Strip of three friends posing together.", title: "Together, in color.",
    note: "Three friends sharing four frames. The full original strip, with every expression kept in view." },
  { id: "weekend", image: "/images/evia/weekend.webp", kind: "portrait", tilt: 8,
    alt: "Friends laughing and holding their photographs outside the booth.", title: "A very good kind of weekend.",
    note: "Friends outside the booth with their printed keepsakes. One of the little moments in our Evia guest album." },
  { id: "keepsakes", image: "/images/evia/keepsakes.webp", kind: "portrait", tilt: -8,
    alt: "Two guests holding their keepsakes beneath the PHOTOBOOTH sign.", title: "Take the feeling home.",
    note: "The booth behind them, their photographs in hand. A small keepsake from a visit to FOTOHAVN at Evia." },
  { id: "guest-sisters", image: "/images/evia/sisters.webp", kind: "portrait", tilt: 9,
    alt: "Two guests holding their prints beside the booth's grey photo display.", title: "A little pause together.",
    note: "Two guests, their photographs in hand, beside a board of earlier moments. From the FOTOHAVN album at Evia." },
  { id: "guest-trio", image: "/images/guest-board/guest-trio.webp", kind: "portrait", tilt: -5,
    alt: "Three guests standing together in front of the cream-curtained booth.", title: "Room for three.",
    note: "Three guests gathered outside the booth, with their prints held close. Another small moment from our Evia guest album." },
  { id: "guest-solo", image: "/images/guest-board/guest-solo.webp", kind: "portrait", tilt: 7,
    alt: "A guest in black holding two Photo Strips beneath the PHOTOBOOTH sign.", title: "Just a moment for you.",
    note: "A guest holding two Photo Strips under the glowing sign. A little pause, and something printed to take home." },
  { id: "guest-duo", image: "/images/guest-board/guest-duo.webp", kind: "portrait", tilt: -7,
    alt: "Two guests, one wearing a blue cap, holding their photographs outside FOTOHAVN.", title: "Something to keep.",
    note: "Side by side outside the booth, with photographs from their visit. A small addition to the FOTOHAVN guest album." },
  { id: "guest-flowers", image: "/images/guest-board/guest-flowers.webp", kind: "portrait", tilt: 6,
    alt: "Two guests with a pink bouquet and printed photographs in front of the booth.", title: "Flowers and photographs.",
    note: "A pink bouquet, printed photographs, and two guests together beneath the sign. A keepsake of a visit to Evia." },
  { id: "guest-three", image: "/images/guest-board/guest-three.webp", kind: "portrait", tilt: -9,
    alt: "Three guests holding Photo Strips beside the booth's Jacobean Walnut exterior.", title: "Three of a kind.",
    note: "Three guests showing their Photo Strips outside the booth. Their expressions join the many little moments on this board." },
  { id: "guest-close", image: "/images/guest-board/guest-close.webp", kind: "portrait", tilt: 5,
    alt: "Two guests in dark clothing holding their Photo Strips beneath the booth sign.", title: "A moment for two.",
    note: "Two guests standing close, each holding a printed memory. From the FOTOHAVN guest album, photographed at Evia." },
  { id: "guest-together", image: "/images/guest-board/guest-together.webp", kind: "portrait", tilt: 8,
    alt: "Two smiling guests holding photographs, one with a blue fan, at the booth.", title: "Good company, kept.",
    note: "Photographs held up and smiles shared outside the booth. Another pair of keepsakes from our time at Evia." },
];

export type BoardState = { fullscreen: boolean; index: number | null; selectedIndex: number; side: "photo" | "note" };
export type BoardAction = { type: "open"; index: number } | { type: "next"; direction: number }
  | { type: "portrait-next"; direction: number }
  | { type: "flip" } | { type: "board" } | { type: "close-photo" } | { type: "exit" };
export const initialBoardState: BoardState = { fullscreen: false, index: null,
  selectedIndex: guestPhotographs.findIndex(photo => photo.id === "guest-trio"), side: "photo" };

export function adjacentPhotograph(index: number, direction: number) {
  return (index + direction % guestPhotographs.length + guestPhotographs.length) % guestPhotographs.length;
}

/** Small and vertical gestures belong to normal page/note scrolling. */
export function photographSwipe(x: number, y: number) {
  return Math.abs(x) >= 40 && Math.abs(x) > Math.abs(y) * 1.4 ? (x < 0 ? 1 : -1) : 0;
}

export function guestBoardReducer(state: BoardState, action: BoardAction): BoardState {
  switch (action.type) {
    case "open": return Number.isInteger(action.index) && action.index >= 0 && action.index < guestPhotographs.length
      ? { ...state, index: action.index, selectedIndex: action.index, side: "photo" } : state;
    case "next": return state.index === null ? state : { ...state,
      index: adjacentPhotograph(state.index, action.direction),
      selectedIndex: adjacentPhotograph(state.index, action.direction), side: "photo" };
    case "portrait-next": return { ...state,
      selectedIndex: adjacentPhotograph(state.selectedIndex, action.direction), side: "photo" };
    case "flip": return { ...state, side: state.side === "photo" ? "note" : "photo" };
    case "board": return { ...state, fullscreen: true, index: null };
    case "close-photo": return { ...state, index: null };
    case "exit": return { ...state, fullscreen: false, index: null };
  }
}
