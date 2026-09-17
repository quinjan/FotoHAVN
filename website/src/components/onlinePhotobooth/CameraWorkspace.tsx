"use client";

import { useEffect, useRef, useState, type CSSProperties, type ReactNode, type RefObject } from "react";
import { ArrowsOutIcon, CameraIcon, CameraRotateIcon, StopIcon, ToggleLeftIcon, ToggleRightIcon, XIcon } from "@phosphor-icons/react";
import { cameraPreviewGeometry } from "./cameraGeometry";
import type { useCamera } from "./useCamera";
import styles from "./CameraWorkspace.module.css";

type Props = {
  active: boolean; videoRef: RefObject<HTMLVideoElement | null>; camera: ReturnType<typeof useCamera>;
  ratio: number; template: string; slots: (string | null)[]; mirror: boolean; onMirror: (value: boolean) => void;
  seconds: number; onSeconds: (value: number) => void; working: boolean; preparing: boolean;
  countdown: { number: number; slot: number } | null; retake: number | null;
  notice: string; renderMessage: ReactNode; onStart: () => void; onStop: () => void; onExit: () => void;
  onImport: () => void; onReview: () => void; onKeepOriginal: () => void;
};

export function CameraWorkspace(props: Props) {
  const { active, videoRef, camera, ratio, slots, working, preparing, countdown, retake } = props;
  const shellRef = useRef<HTMLElement>(null);
  const stageRef = useRef<HTMLDivElement>(null);
  const exitRef = useRef<HTMLButtonElement>(null);
  const [stageSize, setStageSize] = useState({ width: 0, height: 0 });
  const [geometry, setGeometry] = useState<ReturnType<typeof cameraPreviewGeometry>>(null);
  const [fullscreenAvailable, setFullscreenAvailable] = useState(false);
  const [fullscreenNotice, setFullscreenNotice] = useState("");
  const remaining = slots.filter((id) => !id).length;
  const nextSlot = countdown?.slot ?? retake ?? slots.findIndex((id) => !id);
  const controlsLocked = working || preparing || camera.status !== "ready";
  const canFlipCamera = new Set(camera.devices.map((device) => device.deviceId).filter(Boolean)).size > 1;
  const shutterLabel = working ? "Stop capturing" : retake !== null ? "Take replacement" : slots.some(Boolean) ? `Resume ${remaining} photos` : `Start ${remaining} photos`;

  useEffect(() => {
    if (!active) return;
    const element = stageRef.current;
    const shell = shellRef.current;
    if (!element) return;
    const resize = () => setStageSize({ width: element.clientWidth, height: element.clientHeight });
    const observer = new ResizeObserver(resize);
    observer.observe(element);
    const frame = requestAnimationFrame(() => { resize(); exitRef.current?.focus({ preventScroll: true }); setFullscreenAvailable(!!document.fullscreenEnabled); });
    const overflow = document.documentElement.style.overflow;
    document.documentElement.style.overflow = "hidden";
    return () => {
      observer.disconnect(); cancelAnimationFrame(frame); document.documentElement.style.overflow = overflow;
      if (document.fullscreenElement === shell) void document.exitFullscreen().catch(() => {});
    };
  }, [active]);

  useEffect(() => {
    const video = videoRef.current;
    if (!active || !video) return;
    const update = () => setGeometry(cameraPreviewGeometry(video.videoWidth, video.videoHeight, ratio));
    const frame = requestAnimationFrame(update);
    video.addEventListener("loadedmetadata", update); video.addEventListener("resize", update);
    return () => { cancelAnimationFrame(frame); video.removeEventListener("loadedmetadata", update); video.removeEventListener("resize", update); };
  }, [active, ratio, videoRef]);

  async function selectCamera(choice: { deviceId?: string; facingMode?: "user" | "environment" }) {
    if (controlsLocked) return;
    const source = await camera.request(choice);
    if (source) props.onMirror(source.facingMode === "user");
  }

  function flipCamera() {
    if (controlsLocked || !canFlipCamera) return;
    // Desktop cameras without facing metadata cycle through their actual device IDs.
    if (camera.facingMode === "unknown" && camera.devices.length > 1) {
      const index = camera.devices.findIndex((device) => device.deviceId === camera.deviceId);
      void selectCamera({ deviceId: camera.devices[(index + 1) % camera.devices.length].deviceId });
    } else void selectCamera({ facingMode: camera.facingMode === "environment" ? "user" : "environment" });
  }

  async function toggleFullscreen() {
    try {
      if (document.fullscreenElement) await document.exitFullscreen();
      else await shellRef.current?.requestFullscreen();
      setFullscreenNotice("");
    } catch { setFullscreenNotice("Fullscreen isn’t available here. You can keep using the camera in this view."); }
  }

  const width = Math.min(stageSize.width, stageSize.height * ratio);
  return <section ref={shellRef} id="camera-panel" hidden={!active} aria-label="Camera workspace" className={styles.workspace}
    onKeyDown={(event) => {
      if (event.key !== "Escape" || document.fullscreenElement) return;
      event.preventDefault(); event.stopPropagation();
      if (working) props.onStop(); else props.onExit();
    }}>
    <header className={styles.header}>
      <span className={styles.wordmark}>FOTOHAVN</span>
      <div className={styles.sessionInfo}>
        <span>{working ? `Photo ${(countdown?.slot ?? nextSlot) + 1} of ${slots.length}` : retake !== null ? `Retaking photo ${retake + 1}` : `${props.template} · ${slots.length} photos`}</span>
        <div className={styles.progress} aria-label={`${slots.length - remaining} of ${slots.length} photographs captured`}>
          {slots.map((id, index) => <span key={index} className={id ? styles.done : index === nextSlot ? styles.current : ""} />)}
        </div>
      </div>
      <div className={styles.headerActions}>
        {fullscreenAvailable && <button className={styles.expand} type="button" onClick={() => void toggleFullscreen()} aria-label="Toggle fullscreen"><ArrowsOutIcon size={20} aria-hidden="true" /></button>}
        <button ref={exitRef} type="button" onClick={props.onExit} aria-label="Exit camera"><span>Exit</span><XIcon size={18} aria-hidden="true" /></button>
      </div>
    </header>

    <div ref={stageRef} className={styles.stage}>
      <div className={styles.viewfinder} style={{ width: width || "100%", height: width ? width / ratio : "100%", "--count-size": `${Math.min(160, Math.max(72, width * .26))}px` } as CSSProperties}>
        <video ref={videoRef} autoPlay playsInline muted aria-label="Live camera preview" className={`${styles.video} ${props.mirror ? styles.mirrored : ""}`} style={geometry ?? undefined} />
        {camera.status === "ready" && countdown && countdown.number > 0 && <div className={styles.countdown} role="status" aria-live="polite" aria-atomic="true" aria-label={`${countdown.number}`}><span key={countdown.number}>{countdown.number}</span></div>}
        {camera.status === "ready" && countdown?.number === 0 && <div key={countdown.slot} className={styles.flash} aria-hidden="true" />}
      </div>
      {camera.status !== "ready" && <div className={styles.cameraMessage} role="status">
        <CameraIcon size={32} weight="light" aria-hidden="true" />
        <h2>{camera.status === "error" ? "A little interruption." : "Your moment awaits."}</h2>
        <p>{camera.status === "requesting" ? "Allow camera access when your browser asks." : camera.message || "Opening your camera…"}</p>
        {camera.status === "error" && <button type="button" onClick={() => void camera.request()}>Try camera again</button>}
      </div>}
    </div>

    <div className={styles.dock}>
      <div className={`${styles.settings} ${working || preparing ? styles.quiet : ""}`} inert={working || preparing}>
        {camera.devices.length > 1 && camera.facingMode === "unknown" && <label className={styles.source}>Camera<select aria-label="Camera source" value={camera.deviceId} disabled={controlsLocked} onChange={(event) => void selectCamera({ deviceId: event.target.value })}>
          {camera.devices.map((device, index) => <option key={device.deviceId} value={device.deviceId}>{device.label || `Camera ${index + 1}`}</option>)}
        </select></label>}
        <div className={styles.timer} role="group" aria-label="Countdown timer">
          {[3, 5, 10].map((seconds) => <button type="button" key={seconds} aria-pressed={props.seconds === seconds} disabled={controlsLocked} onClick={() => props.onSeconds(seconds)}>{seconds}s</button>)}
        </div>
      </div>
      <div className={styles.actions}>
        <button className={`${styles.support} ${working || preparing ? styles.quiet : ""}`} type="button" role="switch" aria-label="Mirror photographs" aria-checked={props.mirror} disabled={controlsLocked} onClick={() => props.onMirror(!props.mirror)}>
          {props.mirror ? <ToggleRightIcon size={30} weight="fill" aria-hidden="true" /> : <ToggleLeftIcon size={30} weight="light" aria-hidden="true" />}<span>Mirror</span>
        </button>
        <div className={styles.shutterGroup}>
          {remaining > 0 || retake !== null ? <><button id="capture-shutter" type="button" className={styles.shutter} disabled={!working && (controlsLocked || remaining === 0 && retake === null)} aria-label={shutterLabel} onClick={working ? props.onStop : props.onStart}>
            {working && <StopIcon size={26} weight="fill" aria-hidden="true" />}
          </button><span aria-hidden="true">{preparing ? "Preparing your strip…" : working ? "Stop" : retake !== null ? `Take photo ${retake + 1}` : shutterLabel}</span></> : <button className={styles.review} disabled={preparing} type="button" onClick={props.onReview}>Review photos</button>}
        </div>
        <button className={`${styles.support} ${working || preparing ? styles.quiet : ""}`} type="button" disabled={controlsLocked || !canFlipCamera} onClick={flipCamera} aria-label="Flip camera" title={!canFlipCamera ? "No other camera available" : undefined}>
          <CameraRotateIcon size={28} weight="light" aria-hidden="true" /><span>Flip camera</span>
        </button>
      </div>
      <div className={styles.utility}>
        {!working && !preparing && <>
          <button type="button" onClick={props.onImport}>Import photos</button>
          {retake !== null && <button type="button" onClick={props.onKeepOriginal}>Keep original</button>}
          {slots.some(Boolean) && retake === null && <button type="button" onClick={props.onReview}>Review {slots.length - remaining} photos</button>}
        </>}
        {working && <span>{countdown?.number === 0 ? "Captured. A new pose…" : "Make a little moment."}</span>}
      </div>
      {preparing && <div className={styles.preparing}>{props.renderMessage}</div>}
      <p className={styles.notice} role="status">{fullscreenNotice || (!working ? props.notice : "")}</p>
    </div>
  </section>;
}
