"use client";

import Image from "./EventPhoto";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { ArrowCounterClockwise, ArrowsOut, ArrowsIn, ArrowsOutCardinal, Minus, Plus } from "@phosphor-icons/react";
import type { BoothScene, BoothView } from "./booth/createBoothScene";
import { withSiteBasePath } from "../../site.config";
import styles from "./BoothExplorer.module.css";

const views: Array<{ id: BoothView; label: string; description: string }> = [
  { id: "three-quarter", label: "Overview", description: "Jacobean Walnut. Soft light. A little room to be yourselves." },
  { id: "front", label: "Front", description: "The familiar PHOTOBOOTH glow, your photographs on display, and a little hatch for something worth keeping." },
  { id: "side", label: "Side", description: "A proper little enclosure, with the warmth and character of a piece of furniture." },
  { id: "back", label: "Back", description: "Another curtain opening, framed in Jacobean Walnut. The same soft pleats, on the other side." },
  { id: "inside", label: "Inside", description: "A camera set into a Jacobean Walnut panel. A separate screen below, with soft light on either side." },
  { id: "bench", label: "Bench", description: "A Jacobean Walnut seat facing the camera, with a soft curtain behind you. A little room to be yourselves." },
];
const asset = (name: string) => withSiteBasePath(`/images/evia/${name}.webp`);

export default function BoothExplorer() {
  const section = useRef<HTMLElement>(null);
  const host = useRef<HTMLDivElement>(null);
  const scene = useRef<BoothScene | null>(null);
  const studio = useRef<HTMLDivElement>(null);
  const mount = useRef<HTMLDivElement>(null);
  const dialog = useRef<HTMLDialogElement>(null);
  const expandButton = useRef<HTMLButtonElement>(null);
  const inlinePosition = useRef({ height: 0, scrollY: 0 });
  const [status, setStatus] = useState<"idle" | "loading" | "ready" | "unavailable">("idle");
  const [view, setView] = useState<BoothView | "custom">("three-quarter");
  const [captionView, setCaptionView] = useState<BoothView>("three-quarter");
  const [curtainOpen, setCurtainOpen] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [expanded, setExpanded] = useState(false);
  const [mode, setMode] = useState<"rotate" | "move">("rotate");
  const [helpOpen, setHelpOpen] = useState(false);

  useLayoutEffect(() => {
    if (!expanded || !studio.current || !mount.current || !dialog.current) return;
    const content = studio.current, origin = mount.current, modal = dialog.current;
    const trigger = expandButton.current;
    const { height, scrollY } = inlinePosition.current;
    const prior = { position: document.body.style.position, top: document.body.style.top, width: document.body.style.width };
    origin.style.height = `${height}px`;
    document.body.style.position = "fixed";
    document.body.style.top = `-${scrollY}px`;
    document.body.style.width = "100%";
    // Keep one live scene when entering fullscreen; React still owns its children.
    modal.appendChild(content);
    modal.showModal();
    scene.current?.setExpanded(true);
    return () => {
      modal.close();
      origin.appendChild(content);
      origin.style.height = "";
      scene.current?.setExpanded(false);
      Object.assign(document.body.style, prior);
      window.scrollTo({ top: scrollY, behavior: "instant" });
      trigger?.focus({ preventScroll: true });
    };
  }, [expanded]);

  useEffect(() => {
    if (!section.current || !host.current) return;
    let cancelled = false;
    const observer = new IntersectionObserver(([entry]) => {
      if (!entry.isIntersecting) return;
      observer.disconnect();
      setStatus("loading");
      import("./booth/createBoothScene").then(({ createBoothScene }) => {
        if (cancelled || !host.current) return;
        const fail = () => {
          if (cancelled) return;
          scene.current?.dispose();
          scene.current = null;
          setStatus("unavailable");
        };
        try {
          scene.current = createBoothScene(host.current, asset, fail, () => setView("custom"), false, setCurtainOpen);
          setStatus("ready");
        } catch { fail(); }
      }).catch(() => { if (!cancelled) setStatus("unavailable"); });
    }, { rootMargin: "300px" });
    observer.observe(section.current);
    return () => { cancelled = true; observer.disconnect(); scene.current?.dispose(); scene.current = null; };
  }, [attempt]);

  function selectView(next: BoothView) {
    setView(next);
    setCaptionView(next);
    scene.current?.setView(next);
    if (next === "inside" || next === "bench") { setCurtainOpen(true); scene.current?.setCurtain(true); }
  }
  function toggleCurtain() { scene.current?.setCurtain(!curtainOpen); setCurtainOpen(!curtainOpen); }
  function enterFullscreen() {
    inlinePosition.current = { height: studio.current?.getBoundingClientRect().height ?? 0, scrollY: window.scrollY };
    setExpanded(true);
  }
  const ready = status === "ready";
  const description = views.find(item => item.id === captionView)!.description;

  return (
    <section ref={section} id="the-booth" className={styles.section} aria-labelledby="booth-heading">
      <header className={styles.heading}>
        <p className={styles.eyebrow}>THE BOOTH, UP CLOSE</p>
        <h2 id="booth-heading">Small room.<br /><em>Whole other world.</em></h2>
        <p>Come a little closer.</p>
      </header>
      <div ref={mount}>
        <div ref={studio} className={`${styles.studio} ${expanded ? styles.expanded : ""}`}>
          <header className={styles.fullscreenHeader}>
            <span className={styles.wordmark}>FOTOHAVN</span>
            <button type="button" onClick={() => setExpanded(false)} aria-label="Exit full screen">
              <ArrowsIn size={20} aria-hidden="true" /><span>Exit full screen</span>
            </button>
          </header>
          <div className={styles.viewBar}>
            <span className={styles.viewLabel}>Viewpoint</span>
            <div className={styles.viewButtons} role="group" aria-label="Booth viewpoints">
              {views.map(item => <button key={item.id} type="button" disabled={!ready}
                aria-pressed={view === item.id} onClick={() => selectView(item.id)}>{item.label}</button>)}
            </div>
            <select className={styles.viewChooser} aria-label="Booth viewpoint" value={view} disabled={!ready}
              onChange={event => selectView(event.target.value as BoothView)}>
              <option value="custom" disabled>Custom view</option>
              {views.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}
            </select>
          </div>
          <figure className={styles.figure}>
            <div className={styles.stage}>
              <div ref={host} className={styles.canvasHost} role="group"
                aria-label="Interactive 3D model of the FOTOHAVN booth" aria-describedby="booth-instructions"
                tabIndex={ready ? 0 : -1} style={{ visibility: ready ? "visible" : "hidden" }} />
              {!ready && <div className={styles.photoFallback}>
                <Image src={asset("booth-close")} alt="The real FOTOHAVN booth at Evia, with a cream curtain and Jacobean Walnut enclosure."
                  fill sizes="(max-width: 767px) 90vw, 65vw" />
              </div>}
              <button ref={expandButton} type="button" className={styles.expandButton} disabled={!ready}
                onClick={enterFullscreen}><ArrowsOut size={20} aria-hidden="true" />Full screen</button>
            </div>
            <button className={styles.curtainHint} type="button" disabled={!ready} aria-pressed={curtainOpen}
              aria-label={curtainOpen ? "Close curtain" : "Open curtain"} onClick={toggleCurtain}>
              <span className={styles.mouseHint}>Click curtain to {curtainOpen ? "close" : "open"}</span>
              <span className={styles.touchHint}>Tap curtain to {curtainOpen ? "close" : "open"}</span>
            </button>
            <figcaption className={styles.description}>{captionView === "three-quarter" ? <>
              Jacobean Walnut. Soft light.<br className={styles.captionBreak} /> A little room to be yourselves.
            </> : description}</figcaption>
          </figure>
          <div className={styles.controls}>
            <div className={styles.toolbar} role="group" aria-label="Model controls">
              <div className={styles.modeControls}>
                <button type="button" disabled={!ready} aria-pressed={mode === "rotate"}
                  onClick={() => { setMode("rotate"); scene.current?.setMode("rotate"); }}>
                  <ArrowCounterClockwise size={20} aria-hidden="true" />Rotate</button>
                <button type="button" disabled={!ready} aria-pressed={mode === "move"}
                  onClick={() => { setMode("move"); scene.current?.setMode("move"); }}>
                  <ArrowsOutCardinal size={20} aria-hidden="true" />Move</button>
              </div>
              <div className={styles.zoomControls}>
                <button type="button" disabled={!ready} aria-label="Zoom out" onClick={() => scene.current?.zoom(-1)}><Minus size={20} aria-hidden="true" /></button>
                <button type="button" disabled={!ready} aria-label="Zoom in" onClick={() => scene.current?.zoom(1)}><Plus size={20} aria-hidden="true" /></button>
              </div>
              <button type="button" className={styles.reset} disabled={!ready} onClick={() => selectView("three-quarter")}>
                <ArrowCounterClockwise size={20} aria-hidden="true" />Reset</button>
            </div>
            <div className={styles.instructionsRow}>
              <p id="booth-instructions" className={styles.instructions}>
                {ready ? <>
                  <span className={styles.touchHint}>{expanded ? "Drag" : "Drag sideways"} to {mode === "rotate" ? "turn" : "move"}. Two fingers to move or zoom.</span>
                  <span className={styles.mouseHint}>Drag to {mode === "rotate" ? "turn" : "move"}. Two-finger scroll to move. Pinch to zoom.</span>
                </> : status === "unavailable" ? "The 3D view isn’t available. Here’s the booth photographed at Evia." : "Preparing your 3D view…"}
              </p>
              <button className={styles.helpButton} type="button" aria-expanded={helpOpen} aria-controls="booth-help"
                onClick={() => setHelpOpen(!helpOpen)}>{helpOpen ? "Hide help" : "Need help?"}</button>
            </div>
            {helpOpen && <div id="booth-help" className={styles.help}>
              <p>Choose Rotate to turn the booth, or Move to shift the view. Pinch to zoom and use two fingers to move. On the page, swipe vertically to scroll; use a sideways drag to navigate the booth.</p>
              <p>With a mouse or trackpad, click and drag. Two-finger scrolling over the model moves the view without turning it. Scroll outside the model to move the page. Focus the model and use arrow keys to rotate or move, and + or − to zoom. Tap or click the cream curtain to open or close it; the text beneath the booth works too. Reset restores the overview.</p>
            </div>}
            {status === "unavailable" && <button type="button" onClick={() => {
              setView("three-quarter"); setCaptionView("three-quarter"); setCurtainOpen(false); setMode("rotate"); setAttempt(value => value + 1);
            }}>Try 3D again</button>}
            <p className={styles.srOnly} role="status">{ready ? `${view === "custom" ? "Custom" : views.find(item => item.id === view)?.label} view. Curtain ${curtainOpen ? "open" : "closed"}.` : ""}</p>
          </div>
        </div>
      </div>
      <p className={styles.modelDisclaimer}>An illustrated view of our booth, inspired by the real thing. Interior details and proportions are approximate.</p>
      <dialog ref={dialog} className={styles.dialog} aria-label="FOTOHAVN booth full screen"
        onCancel={event => { event.preventDefault(); setExpanded(false); }} />
    </section>
  );
}
