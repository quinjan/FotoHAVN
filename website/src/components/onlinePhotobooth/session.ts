import { getLayout, type FrameId, type LayoutId, type LookId, type TemplateId } from "./presets";

export type Stage = "layout" | "capture" | "look" | "download";
export type Photo = { id: string; url: string; blob: Blob; width: number; height: number; kind: "camera" | "device" };
export type MoveSource = { kind: "slot"; index: number } | { kind: "tray"; id: string };
export type PositionMoveSource = { index: number; photoId: string; revision: number };
export type Operation = { id: number; mode: "sequence" | "import" | "retake"; target: number | null; candidate: string | null };
export type Session = {
  stage: Stage; layoutId: LayoutId; frameId: FrameId; lookId: LookId;
  photos: Record<string, Photo>; slots: (string | null)[]; tray: string[];
  selected: number; operation: Operation | null; move: MoveSource | null; revision: number;
};

export const initialSession: Session = {
  stage: "layout", layoutId: "signature-strip", frameId: "ivory-v1", lookId: "naturale-v1",
  photos: {}, slots: [null, null, null, null], tray: [], selected: 0, operation: null, move: null, revision: 0,
};

export const isComplete = (state: Session) => state.slots.every((id) => id !== null && !!state.photos[id]);
export const canContinue = (state: Session) => isComplete(state) && !state.operation && !state.move;

export type Action =
  | { type: "template"; id: TemplateId }
  | { type: "stage"; stage: Stage }
  | { type: "layout"; id: LayoutId } | { type: "frame"; id: FrameId } | { type: "look"; id: LookId }
  | { type: "select"; index: number }
  | { type: "begin"; id: number; mode: Operation["mode"]; target?: number }
  | { type: "photo"; operationId: number; photo: Photo; index: number }
  | { type: "finish"; id: number } | { type: "cancel" }
  | { type: "accept" } | { type: "try-again" }
  | { type: "move-start"; source: MoveSource } | { type: "move-to"; index: number } | { type: "move-cancel" }
  | { type: "move-position"; source: PositionMoveSource; index: number }
  | { type: "reset" };

function keepReferenced(state: Session): Session {
  const ids = [...state.slots, ...state.tray, state.operation?.candidate].filter((id): id is string => !!id);
  const photos = Object.fromEntries(ids.map((id) => [id, state.photos[id]]));
  return { ...state, photos };
}

function cancelOperation(state: Session) {
  if (!state.operation && !state.move) return state;
  return keepReferenced({ ...state, operation: null, move: null });
}

function placePhoto(state: Session, source: MoveSource, index: number): Session {
  if (state.operation || !Number.isInteger(index) || index < 0 || index >= state.slots.length) return state;
  const slots = [...state.slots]; const tray = [...state.tray];
  if (source.kind === "slot") {
    if (!Number.isInteger(source.index) || !slots[source.index]) return state;
    [slots[source.index], slots[index]] = [slots[index], slots[source.index]];
  } else {
    const from = tray.indexOf(source.id);
    if (from < 0) return state;
    const displaced = slots[index]; slots[index] = source.id;
    if (displaced) tray[from] = displaced; else tray.splice(from, 1);
  }
  return { ...state, slots, tray, selected: index, move: null, revision: state.revision + 1 };
}

export function sessionReducer(state: Session, action: Action): Session {
  switch (action.type) {
    case "reset": return { ...initialSession, revision: state.revision + 1 };
    case "stage": {
      if ((action.stage === "look" || action.stage === "download") && !canContinue(state)) return state;
      return { ...cancelOperation(state), stage: action.stage };
    }
    case "template":
    case "layout": {
      if (state.operation || action.id === state.layoutId) return state;
      const pool = [...state.slots.filter((id): id is string => !!id), ...state.tray];
      const count = getLayout(action.id).slots.length;
      return { ...state, layoutId: action.id, frameId: action.type === "template" ? "ivory-v1" : state.frameId, slots: Array.from({ length: count }, (_, i) => pool[i] ?? null), tray: pool.slice(count), selected: 0, move: null, revision: state.revision + 1 };
    }
    case "frame": return state.operation || action.id === state.frameId ? state : { ...state, frameId: action.id, revision: state.revision + 1 };
    case "look": return state.operation || action.id === state.lookId ? state : { ...state, lookId: action.id, revision: state.revision + 1 };
    case "select": return state.operation || action.index < 0 || action.index >= state.slots.length ? state : { ...state, selected: action.index };
    case "begin": {
      if (state.stage !== "capture" || state.operation) return state;
      if (action.mode === "retake" && (action.target === undefined || !state.slots[action.target])) return state;
      return { ...state, move: null, selected: action.mode === "retake" ? action.target! : state.selected, operation: { id: action.id, mode: action.mode, target: action.target ?? null, candidate: null } };
    }
    case "photo": {
      const operation = state.operation;
      if (!operation || operation.id !== action.operationId || action.index < 0 || action.index >= state.slots.length || state.photos[action.photo.id]) return state;
      if (operation.mode === "retake") {
        if (operation.target !== action.index || operation.candidate) return state;
        return { ...state, photos: { ...state.photos, [action.photo.id]: action.photo }, operation: { ...operation, candidate: action.photo.id } };
      }
      if (state.slots[action.index]) return state;
      const slots = [...state.slots]; slots[action.index] = action.photo.id;
      return { ...state, slots, photos: { ...state.photos, [action.photo.id]: action.photo }, selected: action.index, revision: state.revision + 1 };
    }
    case "finish": return state.operation?.id === action.id && state.operation.mode !== "retake" ? { ...state, operation: null } : state;
    case "cancel": return cancelOperation(state);
    case "accept": {
      const operation = state.operation;
      if (!operation?.candidate || operation.target === null) return state;
      const slots = [...state.slots]; slots[operation.target] = operation.candidate;
      return keepReferenced({ ...state, slots, selected: operation.target, operation: null, revision: state.revision + 1 });
    }
    case "try-again": return state.operation?.candidate ? keepReferenced({ ...state, operation: { ...state.operation, candidate: null } }) : state;
    case "move-start": {
      if (state.operation) return state;
      const valid = action.source.kind === "slot" ? !!state.slots[action.source.index] : state.tray.includes(action.source.id);
      return valid ? { ...state, move: action.source } : state;
    }
    case "move-to": return state.move ? placePhoto(state, state.move, action.index) : state;
    case "move-position": {
      // A pointer gesture owns a source snapshot, not a pending React move render.
      // Reject a late release after a retake, layout change, or other placement.
      if (state.stage !== "capture" || state.operation || state.move || state.revision !== action.source.revision || state.slots[action.source.index] !== action.source.photoId) return state;
      return placePhoto(state, { kind: "slot", index: action.source.index }, action.index);
    }
    case "move-cancel": return { ...state, move: null };
  }
}
