"use client";

import { useEffect, useMemo, useState } from "react";
import { loadCompositionFonts, renderComposition } from "./compositor";
import { decodeImage } from "./media";
import { compositionFilename } from "./download";
import type { Session } from "./session";

type Rendered = { url: string; blob: Blob; revision: number; filename: string; source: object };
export function useComposition(state: Session, reviewCandidate = false) {
  const [rendered, setRendered] = useState<Rendered | null>(null);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const composition = useMemo(() => ({
    layoutId: state.layoutId, frameId: state.frameId, lookId: state.lookId,
    photos: state.slots.map((id, index) => reviewCandidate && state.operation?.target === index && state.operation.candidate ? state.photos[state.operation.candidate] : id ? state.photos[id] : null),
    revision: state.revision,
  }), [state.layoutId, state.frameId, state.lookId, state.slots, state.photos, state.revision, reviewCandidate, state.operation]);

  useEffect(() => {
    const abort = new AbortController();
    void loadCompositionFonts().then((fonts) => renderComposition(composition, fonts, abort.signal)).then(async (blob) => {
      if (abort.signal.aborted) return;
      const url = URL.createObjectURL(blob);
      try { await decodeImage(url, abort.signal); }
      catch (error) { URL.revokeObjectURL(url); throw error; }
      if (abort.signal.aborted) { URL.revokeObjectURL(url); return; }
      setError("");
      setRendered({ url, blob, revision: composition.revision, source: composition, filename: compositionFilename(composition.layoutId) });
    }).catch((cause: unknown) => {
      if (!abort.signal.aborted) setError(cause instanceof Error ? cause.message : "The preview couldn’t be prepared. Please try again.");
    });
    return () => abort.abort();
  }, [composition, attempt]);

  useEffect(() => {
    if (!rendered) return;
    return () => URL.revokeObjectURL(rendered.url);
  }, [rendered]);

  return { ...rendered, error, ready: rendered?.source === composition, retry: () => { setError(""); setAttempt((value) => value + 1); } };
}
