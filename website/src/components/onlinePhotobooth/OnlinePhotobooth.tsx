"use client";

/* Captures and composed PNGs are in-memory blob URLs, never optimizer requests. */
/* eslint-disable @next/next/no-img-element */
import { useCallback, useEffect, useReducer, useRef, useState, type ChangeEvent } from "react";
import { ArrowLeftIcon, ArrowRightIcon, DownloadSimpleIcon, CheckIcon, XIcon } from "@phosphor-icons/react";
import { findBoothSectionUrl, withSiteBasePath } from "../../../site.config";
import { getLayout, looks } from "./presets";
import { canContinue, initialSession, isComplete, sessionReducer, type MoveSource, type Photo, type PositionMoveSource, type Stage } from "./session";
import { disposePhoto, photoFromFile, photoFromVideo, waitFor } from "./media";
import { CameraWorkspace } from "./CameraWorkspace";
import { useCamera } from "./useCamera";
import { useComposition } from "./useComposition";
import { PrintPreview } from "./PrintPreview";
import { TemplateStep } from "./TemplateStep";
import styles from "./OnlinePhotobooth.module.css";

const stages: { id: Stage; name: string }[] = [
  { id: "layout", name: "Template" }, { id: "capture", name: "Photographs" },
  { id: "look", name: "Filters" }, { id: "download", name: "Download" },
];
const photoWord = (count: number) => count === 1 ? "photograph" : "photographs";

export default function OnlinePhotobooth() {
  const [state, dispatch] = useReducer(sessionReducer, initialSession);
  const [notice, setNotice] = useState("");
  const [countdown, setCountdown] = useState<{ number: number; slot: number } | null>(null);
  const [working, setWorking] = useState(false);
  const [captureTab, setCaptureTab] = useState<"camera" | "arrange">("camera");
  const [replacementSource, setReplacementSource] = useState<"camera" | "device">("camera");
  const [reviewWhenReady, setReviewWhenReady] = useState(false);
  const [hasSelection, setHasSelection] = useState(false);
  const [mirror, setMirror] = useState(true);
  const [countdownSeconds, setCountdownSeconds] = useState(3);
  const [moveDestination, setMoveDestination] = useState(0);
  const [confirmReset, setConfirmReset] = useState(false);
  const videoRef = useRef<HTMLVideoElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const fileTarget = useRef<number | null>(null);
  const operationCounter = useRef(0);
  const pending = useRef<AbortController | null>(null);
  const busy = useRef(false);
  const resources = useRef(new Map<string, Photo>());
  const priorPhotos = useRef(state.photos);
  const stateRef = useRef(state);
  const pendingFocus = useRef<number | null>(null);

  useEffect(() => { stateRef.current = state; }, [state]);

  const cancelPending = useCallback(() => {
    pending.current?.abort(); pending.current = null;
    busy.current = false; setWorking(false); setCountdown(null);
    dispatch({ type: "cancel" });
  }, []);

  const interrupted = useCallback(() => {
    cancelPending(); setNotice("Capture stopped. Your accepted photographs are still here.");
  }, [cancelPending]);
  const camera = useCamera(videoRef, interrupted);
  const stopCamera = camera.stop;
  const print = useComposition(state, true);
  const layout = getLayout(state.layoutId);
  const look = looks.find((item) => item.id === state.lookId)!;
  const complete = isComplete(state);
  const remaining = state.slots.filter((id) => !id).length;
  const candidate = state.operation?.candidate ? state.photos[state.operation.candidate] : null;
  const retaking = state.operation?.mode === "retake";
  const target = retaking ? state.operation!.target! : null;
  const locked = !!state.operation;
  const selectedPhoto = state.slots[state.selected] ? state.photos[state.slots[state.selected]!] : null;

  useEffect(() => {
    for (const [id, photo] of Object.entries(priorPhotos.current)) {
      if (!state.photos[id]) { disposePhoto(photo); resources.current.delete(id); }
    }
    priorPhotos.current = state.photos;
  }, [state.photos]);

  useEffect(() => {
    const ownedResources = resources.current;
    return () => { pending.current?.abort(); ownedResources.forEach(disposePhoto); ownedResources.clear(); };
  }, []);

  const requestCamera = camera.request;
  useEffect(() => {
    if (state.stage !== "capture" || captureTab !== "camera") return;
    void requestCamera();
    return stopCamera;
  }, [state.stage, captureTab, requestCamera, stopCamera]);

  useEffect(() => {
    const visibility = () => {
      if (document.hidden && busy.current) {
        cancelPending(); setNotice("Capture stopped.");
      }
    };
    document.addEventListener("visibilitychange", visibility);
    return () => document.removeEventListener("visibilitychange", visibility);
  }, [cancelPending]);

  useEffect(() => {
    if (reviewWhenReady && print.ready && !print.error && !working) {
      const frame = requestAnimationFrame(() => {
        setReviewWhenReady(false); setCaptureTab("arrange");
        requestAnimationFrame(() => document.getElementById(candidate ? "accept-photograph" : "arrange-tab")?.focus());
      });
      return () => cancelAnimationFrame(frame);
    }
  }, [reviewWhenReady, print.ready, print.error, working, candidate]);

  useEffect(() => {
    headingRef.current?.focus({ preventScroll: true });
  }, [state.stage]);

  const focusSlot = (index: number) => { pendingFocus.current = index; };

  useEffect(() => {
    if (!print.ready || state.operation || state.stage !== "capture" || captureTab !== "arrange" || pendingFocus.current === null) return;
    const frame = requestAnimationFrame(() => {
      const index = pendingFocus.current;
      const overlay = document.getElementById(`photograph-position-${index}`) as HTMLButtonElement | null;
      const rail = document.getElementById(`photograph-rail-${index}`) as HTMLButtonElement | null;
      const button = overlay?.getClientRects().length ? overlay : rail;
      if (button && !button.disabled) { button.focus({ preventScroll: true }); pendingFocus.current = null; }
    });
    return () => cancelAnimationFrame(frame);
  }, [print.ready, state.operation, state.stage, state.revision, state.move, captureTab]);

  function changeCaptureTab(tab: "camera" | "arrange") {
    if (tab === captureTab || working || (reviewWhenReady && !print.error)) return;
    setReviewWhenReady(false);
    if (state.operation) cancelPending();
    if (state.move) dispatch({ type: "move-cancel" });
    setCaptureTab(tab); setNotice("");
  }

  function goTo(stage: Stage) {
    if (stage === state.stage || working) return;
    setReviewWhenReady(false);
    if (stage === "capture") setCaptureTab(complete ? "arrange" : "camera");
    if ((stage === "look" || stage === "download") && (!canContinue(state) || !print.ready || print.error)) return;
    cancelPending(); camera.stop(); pendingFocus.current = null; setNotice(""); setConfirmReset(false);
    dispatch({ type: "stage", stage });
    requestAnimationFrame(() => headingRef.current?.scrollIntoView({ block: "start", behavior: "instant" }));
  }

  function makeOperation(mode: "sequence" | "import" | "retake", slot?: number) {
    pending.current?.abort();
    const controller = new AbortController(); pending.current = controller;
    const id = ++operationCounter.current;
    dispatch({ type: "cancel" });
    dispatch({ type: "begin", mode, id, target: slot });
    return { id, controller };
  }

  function retain(photo: Photo) { resources.current.set(photo.id, photo); }

  function beginRetake() {
    if (!selectedPhoto || locked || busy.current) return;
    setReplacementSource("camera"); setCaptureTab("camera");
    makeOperation("retake", state.selected);
    setNotice("Your original is kept until you accept.");
  }

  async function capture() {
    if (captureTab !== "camera" || busy.current || camera.status !== "ready" || !videoRef.current || candidate || state.move) return;
    const positions = retaking ? [target!] : state.slots.flatMap((id, index) => id ? [] : [index]);
    if (!positions.length) return;
    const operation = retaking && state.operation && pending.current
      ? { id: state.operation.id, controller: pending.current }
      : makeOperation("sequence");
    const { id, controller } = operation;
    busy.current = true; setWorking(true); setNotice("");
    try {
      for (const slot of positions) {
        for (let number = countdownSeconds; number > 0; number--) {
          setCountdown({ number, slot });
          await waitFor(1000, controller.signal);
        }
        if (document.hidden || !videoRef.current) throw new Error("Capture paused. Resume the camera when you’re ready.");
        setCountdown(null);
        const photo = await photoFromVideo(videoRef.current, mirror, controller.signal);
        if (controller.signal.aborted) { disposePhoto(photo); return; }
        retain(photo); dispatch({ type: "photo", operationId: id, photo, index: slot });
        setCountdown({ number: 0, slot });
        setNotice(`Photograph ${slot + 1} ${retaking ? "is ready to review" : "captured"}.`);
        // Give the shutter feedback its own beat before another countdown or review.
        // The same cancellation signal keeps Stop responsive during this interval.
        await waitFor(800, controller.signal);
      }
      setReviewWhenReady(true);
      if (!retaking) {
        dispatch({ type: "finish", id });
        setNotice("Your photographs are ready. Select one to retake it, or move them into your favorite order.");
        requestAnimationFrame(() => headingRef.current?.focus({ preventScroll: true }));
      }
    } catch (error) {
      if (!controller.signal.aborted) {
        dispatch({ type: "cancel" });
        setNotice(error instanceof Error ? error.message : "The photograph couldn’t be taken. Your accepted photographs are safe.");
      }
    } finally {
      if (pending.current === controller) { busy.current = false; setWorking(false); setCountdown(null); }
    }
  }

  function openFiles(slot: number | null) {
    if (busy.current) return;
    setReplacementSource("device");
    fileTarget.current = slot;
    if (fileRef.current) {
      fileRef.current.value = ""; fileRef.current.multiple = slot === null; fileRef.current.click();
    }
  }

  async function importFiles(event: ChangeEvent<HTMLInputElement>) {
    await importPhotographs(Array.from(event.target.files ?? []), fileTarget.current);
  }

  async function importPhotographs(files: File[], slot: number | null) {
    if (!files.length || stateRef.current.stage !== "capture" || busy.current) return;
    setReplacementSource("device");
    setCaptureTab("arrange");
    const replacing = slot !== null && !!stateRef.current.slots[slot];
    const positions = slot !== null ? [slot] : state.slots.flatMap((id, index) => id ? [] : [index]);
    if (!positions.length) return;
    const { id, controller } = makeOperation(replacing ? "retake" : "import", slot ?? undefined);
    busy.current = true; setWorking(true); setNotice("Preparing your photographs on this device…");
    let accepted = 0;
    const errors: string[] = [];
    try {
      for (const file of files) {
        if (accepted >= positions.length) break;
        try {
          const photo = await photoFromFile(file, controller.signal);
          if (controller.signal.aborted) { disposePhoto(photo); return; }
          retain(photo); dispatch({ type: "photo", operationId: id, photo, index: positions[accepted] }); accepted += 1;
        } catch (error) {
          if (controller.signal.aborted) return;
          errors.push(error instanceof Error ? error.message : `${file.name} couldn’t be opened.`);
        }
      }
      if (!replacing) dispatch({ type: "finish", id });
      else if (!accepted) dispatch({ type: "cancel" });
      if (accepted) { setReviewWhenReady(true); setHasSelection(replacing); }
      const status = replacing && accepted ? "Your replacement is ready. Keep it, or keep the original." : `${accepted} ${photoWord(accepted)} added.`;
      setNotice(`${status}${files.length > positions.length ? ` This layout had room for ${positions.length} ${photoWord(positions.length)}.` : ""}${errors.length ? ` ${errors.join(" ")}` : ""}`);
      if (accepted && slot === null) requestAnimationFrame(() => headingRef.current?.focus({ preventScroll: true }));
    } finally {
      if (pending.current === controller) { busy.current = false; setWorking(false); }
    }
  }

  function cancelRetake() {
    const originalSlot = target ?? state.selected;
    setReviewWhenReady(false); setCaptureTab("arrange");
    cancelPending(); setNotice(`Kept the original photograph ${originalSlot + 1}.`); focusSlot(originalSlot);
  }

  function acceptRetake() {
    const originalSlot = target!;
    pending.current?.abort(); pending.current = null;
    setCaptureTab("arrange"); setHasSelection(true);
    dispatch({ type: "accept" }); setNotice(`Photograph ${originalSlot + 1} replaced. The others are unchanged.`); focusSlot(originalSlot);
  }

  function startMove(source: MoveSource) {
    dispatch({ type: "move-start", source }); setMoveDestination(state.selected);
    setNotice("Choose a position in the frame, or choose a destination below. Occupied positions swap photographs.");
  }

  function applyMove(index: number) {
    dispatch({ type: "move-to", index });
    setNotice(`Photograph moved to position ${index + 1}${state.slots[index] ? "; the other photograph swapped places" : ""}.`);
    focusSlot(index);
  }

  function reorderPosition(source: PositionMoveSource, index: number) {
    const current = stateRef.current;
    if (busy.current || current.operation || current.move || current.stage !== "capture" || current.revision !== source.revision || current.slots[source.index] !== source.photoId) return;
    setHasSelection(true);
    dispatch({ type: "move-position", source, index });
    setNotice(`Photograph moved to position ${index + 1}${current.slots[index] ? "; the other photograph swapped places" : ""}.`);
    focusSlot(index);
  }

  function cancelMove() {
    const slot = state.move?.kind === "slot" ? state.move.index : state.selected;
    dispatch({ type: "move-cancel" }); setNotice("Move cancelled."); focusSlot(slot);
  }

  function selectSlot(index: number) {
    setHasSelection(true);
    if (state.move) applyMove(index); else dispatch({ type: "select", index });
  }

  function reset() {
    cancelPending(); camera.stop(); pendingFocus.current = null; setNotice(""); setConfirmReset(false);
    dispatch({ type: "reset" });
    requestAnimationFrame(() => headingRef.current?.scrollIntoView({ block: "start", behavior: "instant" }));
  }

  const preview = (interactive = false, reveal = false) => <PrintPreview state={state} url={print.url} ready={print.ready} interactive={interactive} reveal={reveal}
    onSelect={selectSlot} onReorder={reorderPosition} showSelection={hasSelection || !!state.move || !!candidate}
    onFiles={(files, index) => { if (!locked && !state.move && !busy.current) void importPhotographs(files, index); }}
    onDragCancel={(index) => { setNotice("Move cancelled."); focusSlot(index); }} />;

  const renderMessage = <div className={styles.renderStatus} aria-live="polite">
    {print.error ? <><p>{print.error}</p><button className={styles.textButton} type="button" onClick={print.retry}>Try preview again</button></> : !print.ready ? <p>Preparing your framed preview…</p> : null}
  </div>;

  return <div className={`${styles.booth} ${state.stage === "capture" && captureTab === "camera" ? styles.cameraMode : ""}`} onKeyDown={(event) => {
    if (event.key === "Escape" && state.move) { event.preventDefault(); cancelMove(); }
    else if (event.key === "Escape" && state.operation) { event.preventDefault(); if (retaking) cancelRetake(); else cancelPending(); }
  }}>
    <a className="skipLink" href="#online-workspace">Skip to the online booth</a>
    <header className={styles.header}>
      <a href={withSiteBasePath("/")} className={styles.wordmark} aria-label="FOTOHAVN home">FOTOHAVN</a>
      <span className={styles.headerLabel}>THE ONLINE BOOTH</span>
      <a href={withSiteBasePath("/#experience")} className={styles.backLink}><ArrowLeftIcon size={16} aria-hidden="true" /><span>BACK TO FOTOHAVN</span></a>
    </header>

    <main id="online-workspace" className={`${styles.main} ${state.stage === "capture" ? styles.photographsMain : ""}`}>
      <nav aria-label="Photobooth stages" className={styles.progress}>
        {stages.map((stage, index) => <button key={stage.id} type="button" onClick={() => goTo(stage.id)}
          aria-current={state.stage === stage.id ? "step" : undefined}
          disabled={working || (index > 1 && (!canContinue(state) || !print.ready || !!print.error)) || (stage.id === "download" && state.stage !== "download" && state.stage !== "look")}
          className={state.stage === stage.id ? styles.currentStep : ""}>
          <span className={styles.stepNumber}>{String(index + 1).padStart(2, "0")}</span>{stage.name}
        </button>)}
      </nav>

      <div className={`${styles.stageContent} ${state.stage === "layout" ? styles.templateWorkspace : ""}`} key={state.stage}>
        <div className={styles.intro}>
          <h1 ref={headingRef} tabIndex={-1} className={styles.heading}>
            {state.stage === "layout" && <>Make a little <em>Moment</em></>}
            {state.stage === "capture" && <>Find your <em>light.</em></>}
            {state.stage === "look" && <>Find the <em>feeling.</em></>}
            {state.stage === "download" && <>A little moment. <em>Yours.</em></>}
          </h1>
          <p>{state.stage === "layout" ? "choose your template" : state.stage === "capture" ? "Let this moment become a memory." : state.stage === "look" ? "Choose a filter for your photographs." : "Your digital keepsake, ready to take with you."}</p>
        </div>

        {state.stage === "layout" && <TemplateStep selectedId={state.layoutId} onSelect={(id) => dispatch({ type: "template", id })} onContinue={() => goTo("capture")} kept={state.tray.length} photographPreview={state.slots.some((id) => id !== null) ? print : undefined} />}

        {state.stage === "capture" && <>
          <div className={styles.captureTabs} role="tablist" aria-label="Photograph workspace">
            {(["camera", "arrange"] as const).map((tab) => <button key={tab} id={`${tab}-tab`} role="tab" type="button" aria-selected={captureTab === tab} aria-controls={`${tab}-panel`} tabIndex={captureTab === tab ? 0 : -1} disabled={working || (reviewWhenReady && !print.error)} onClick={() => changeCaptureTab(tab)} onKeyDown={(event) => {
              if (["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
                event.preventDefault(); const next = event.key === "Home" ? "camera" : event.key === "End" ? "arrange" : tab === "camera" ? "arrange" : "camera";
                changeCaptureTab(next); document.getElementById(`${next}-tab`)?.focus();
              }
            }}>{tab === "camera" ? "Camera" : "Arrange"}</button>)}
          </div>
          <CameraWorkspace active={captureTab === "camera"} videoRef={videoRef} camera={camera}
            ratio={layout.slots[0].width / layout.slots[0].height} template={layout.name} slots={state.slots}
            mirror={mirror} onMirror={setMirror} seconds={countdownSeconds} onSeconds={setCountdownSeconds}
            working={working} preparing={reviewWhenReady} countdown={countdown} retake={target}
            notice={notice} renderMessage={renderMessage} onStart={() => void capture()}
            onStop={() => { cancelPending(); if (retaking) makeOperation("retake", target!); setNotice("Stopped. Your photographs are still here."); }}
            onExit={() => {
              cancelPending(); camera.stop(); setReviewWhenReady(false); setNotice("");
              if (retaking) cancelRetake();
              else if (state.slots.some(Boolean)) setCaptureTab("arrange");
              else dispatch({ type: "stage", stage: "layout" });
              requestAnimationFrame(() => document.getElementById("arrange-tab")?.focus());
            }}
            onImport={() => openFiles(retaking ? target : null)} onReview={() => changeCaptureTab("arrange")} onKeepOriginal={cancelRetake} />
          <section id="arrange-panel" role="tabpanel" aria-labelledby="arrange-tab" hidden={captureTab !== "arrange"} className={styles.arrangeCanvas}>
            {preview(true)}
            {candidate ? <div className={styles.candidateActions}>
              <p>Replacement for photograph {target! + 1}</p>
              <div className={styles.actionRow}>
                <button id="accept-photograph" type="button" className={styles.primary} disabled={!print.ready || !!print.error} onClick={acceptRetake}>Use photograph<CheckIcon size={18} aria-hidden="true" /></button>
                <button type="button" className={styles.textButton} onClick={() => {
                  if (replacementSource === "device") openFiles(target);
                  else { dispatch({ type: "try-again" }); setCaptureTab("camera"); setReviewWhenReady(false); setNotice(""); }
                }}>{replacementSource === "device" ? "Choose another" : "Try again"}</button>
                <button type="button" className={styles.textButton} onClick={cancelRetake}>Keep original</button>
              </div>
            </div> : <>
              {hasSelection && !locked && selectedPhoto && !state.move && <div className={styles.selectionActions} aria-label={`Edit photograph ${state.selected + 1}`}>
                <button type="button" className={styles.textButton} onClick={beginRetake}>Retake</button>
                <button type="button" className={styles.textButton} onClick={() => openFiles(state.selected)}>Replace</button>
                <button type="button" className={styles.textButton} onClick={() => startMove({ kind: "slot", index: state.selected })}>Move</button>
              </div>}
              <p className={styles.arrangeHint}>Drag to rearrange · Tap a photograph to edit</p>
            </>}
            {working && <button type="button" className={styles.secondary} onClick={cancelPending}>Cancel import<XIcon size={18} aria-hidden="true" /></button>}
            {state.move && <div className={styles.moveControls}>
              <p>Tap a position in the template. Occupied photographs swap places.</p>
              <label htmlFor="move-destination">Move to position</label>
              <div className={styles.actionRow}><select id="move-destination" value={moveDestination} onChange={(event) => setMoveDestination(Number(event.target.value))}>{state.slots.map((id, index) => <option key={index} value={index}>Photograph {index + 1}{id ? " — swap" : " — empty"}</option>)}</select><button type="button" className={styles.secondary} onClick={() => applyMove(moveDestination)}>Apply move</button><button type="button" className={styles.textButton} onClick={cancelMove}>Cancel</button></div>
            </div>}
            {renderMessage}
            <div className={styles.arrangeMeta}>
              {remaining > 0 && !locked && <button type="button" className={styles.textButton} onClick={() => openFiles(null)}>Import photos</button>}
              {state.tray.length > 0 && <details className={styles.keptPhotos}><summary>Kept photos ({state.tray.length})</summary>
                <div className={styles.trayPhotos}>{state.tray.map((id, index) => <button key={id} type="button" disabled={locked} aria-pressed={state.move?.kind === "tray" && state.move.id === id} onClick={() => startMove({ kind: "tray", id })} aria-label={`Place retained photograph ${index + 1}`}><img src={state.photos[id].url} width={100} height={75} alt={`Retained photograph ${index + 1}`} /><span>Place photograph {index + 1}</span></button>)}</div>
              </details>}
              <p>{layout.name} · {state.slots.length - remaining} of {state.slots.length} ready</p>
            </div>
          </section>
          <div className={styles.captureFooter}>
            <button type="button" className={styles.textButton} disabled={working || (reviewWhenReady && !print.error)} onClick={() => goTo("layout")}><ArrowLeftIcon size={16} aria-hidden="true" />Change template</button>
            {captureTab === "arrange" && <button type="button" className={styles.primary} disabled={!canContinue(state) || !print.ready || !!print.error} onClick={() => goTo("look")}>Continue to Filters<ArrowRightIcon size={18} aria-hidden="true" /></button>}
          </div>
        </>}

        {state.stage === "look" && <div className={styles.workspace}>
          <div className={styles.previewColumn}>{preview()}<p className={styles.previewCaption}>{layout.name}<span> / </span>{look.name}</p>{renderMessage}</div>
          <div className={styles.controls}>
            <fieldset className={`${styles.fieldset} ${styles.looksFieldset}`}>
              <legend>Choose your filter</legend>
              <div className={styles.lookOptions}>{looks.map((item, index) => <label key={item.id} className={`${styles.lookOption} ${state.lookId === item.id ? styles.checked : ""}`}>
                <input type="radio" name="filter" value={item.id} checked={state.lookId === item.id} onChange={() => dispatch({ type: "look", id: item.id })} />
                <span className={styles.lookNumber}>{String(index + 1).padStart(2, "0")}</span><span><span className={styles.lookTitle}>{item.name}</span><span className={styles.lookDescription}>{item.description}</span></span><CheckIcon className={styles.check} size={18} aria-hidden="true" />
              </label>)}</div>
            </fieldset>
            <p className={styles.starterNote}>Filters affect your photographs only. You can change the filter at any time.</p>
            <div className={styles.nextAction}><button type="button" className={styles.primary} disabled={!print.ready || !!print.error} onClick={() => goTo("download")}>Make my photostrip<ArrowRightIcon size={18} aria-hidden="true" /></button></div>
            <div className={styles.actionRow}><button type="button" className={styles.textButton} onClick={() => goTo("capture")}>Edit photographs</button><button type="button" className={styles.textButton} onClick={() => goTo("layout")}>Change template</button></div>
          </div>
        </div>}

        {state.stage === "download" && <div className={styles.workspace}>
          <div className={styles.previewColumn}>{preview(false, true)}<p className={styles.previewCaption}>{layout.width} × {layout.height} pixels<span> / </span>PNG</p>{renderMessage}</div>
          <div className={`${styles.controls} ${styles.downloadControls}`}>
            <p className={styles.finishedNote}>A small record<br />of <em>right now.</em></p>
            <p>{layout.slots.length} photographs. {layout.name} template. {look.name} filter.<br />Made by you, with FOTOHAVN.</p>
            {print.url && print.ready && <a className={styles.primary} href={print.url} download={print.filename}><DownloadSimpleIcon size={20} aria-hidden="true" />Download PNG</a>}
            {print.url && <a className={styles.textButton} href={print.url} target="_blank" rel="noreferrer">Open image to save</a>}
            <div className={styles.actionRow}><button type="button" className={styles.secondary} onClick={() => goTo("look")}>Edit your strip</button><button type="button" className={styles.textButton} onClick={() => setConfirmReset(true)}>Make another</button></div>
            {confirmReset && <div className={styles.resetConfirmation}><p>Start a fresh strip? Download this one first if you’d like to keep it.</p><div className={styles.actionRow}><button type="button" className={styles.secondary} onClick={reset}>Start a new strip</button><button type="button" className={styles.textButton} onClick={() => setConfirmReset(false)}>Keep this one</button></div></div>}
            <a className={styles.physicalLink} href={findBoothSectionUrl}>There’s a little room for you in person, too.<ArrowRightIcon size={18} aria-hidden="true" /></a>
          </div>
        </div>}
      </div>
      <input ref={fileRef} className={styles.fileInput} type="file" accept="image/jpeg,image/png,image/webp,image/avif,image/heic,image/heif" multiple onChange={(event) => void importFiles(event)} tabIndex={-1} aria-label="Choose device photographs" />
      <p className={styles.notice} role="status" aria-live="polite" aria-atomic="true">{notice}</p>
    </main>
    <footer className={styles.footer}><span>PHOTOGRAPHS, DEVELOPED DIFFERENTLY.</span><p>Made on your device. Nothing uploaded. This session ends when you leave or refresh.</p></footer>
  </div>;
}
