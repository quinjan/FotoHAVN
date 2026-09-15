"use client";

import Image from "./EventPhoto";
import { useEffect, useRef, useState } from "react";
import { ArrowUUpLeft, ArrowUUpRight, Minus, Plus } from "@phosphor-icons/react";
import type { BoothScene, BoothView } from "./booth/createBoothScene";
import { withSiteBasePath } from "../../site.config";
import styles from "./BoothExplorer.module.css";

const views: Array<{ id: BoothView; label: string; description: string }> = [
  {
    id: "three-quarter",
    label: "Overview",
    description:
      "Jacobean Walnut. Soft light. A cream curtain between you and the rest of the room.",
  },
  {
    id: "front",
    label: "Front",
    description:
      "The familiar PHOTOBOOTH glow, your photographs on display, and a little hatch for something worth keeping.",
  },
  {
    id: "side",
    label: "Side",
    description:
      "A proper little enclosure, with the warmth and character of a piece of furniture.",
  },
  {
    id: "back",
    label: "Back",
    description:
      "Another curtain opening, framed in Jacobean Walnut. The same soft pleats, on the other side.",
  },
  {
    id: "inside",
    label: "Inside",
    description:
      "A camera set into a Jacobean Walnut panel. A separate screen below, with soft light on either side.",
  },
  {
    id: "bench",
    label: "Bench",
    description:
      "A Jacobean Walnut seat facing the camera, with a soft curtain behind you. A little room to be yourselves.",
  },
];
const asset = (name: string) => withSiteBasePath(`/images/evia/${name}.webp`);

export default function BoothExplorer() {
  const section = useRef<HTMLElement>(null);
  const host = useRef<HTMLDivElement>(null);
  const scene = useRef<BoothScene | null>(null);
  const [status, setStatus] = useState<
    "idle" | "loading" | "ready" | "unavailable"
  >("idle");
  const [view, setView] = useState<BoothView | "custom">("three-quarter");
  const [curtainOpen, setCurtainOpen] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!section.current || !host.current) return;
    let cancelled = false;
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting) return;
        observer.disconnect();
        setStatus("loading");
        import("./booth/createBoothScene")
          .then(({ createBoothScene }) => {
            if (cancelled || !host.current) return;
            const fail = () => {
              if (cancelled) return;
              scene.current?.dispose();
              scene.current = null;
              setStatus("unavailable");
            };
            try {
              scene.current = createBoothScene(host.current, asset, fail, () =>
                setView("custom"),
              );
              setStatus("ready");
            } catch {
              fail();
            }
          })
          .catch(() => {
            if (!cancelled) setStatus("unavailable");
          });
      },
      { rootMargin: "300px" },
    );
    observer.observe(section.current);
    return () => {
      cancelled = true;
      observer.disconnect();
      scene.current?.dispose();
      scene.current = null;
    };
  }, [attempt]);

  function selectView(next: BoothView) {
    setView(next);
    scene.current?.setView(next);
    if (next === "inside" || next === "bench") {
      setCurtainOpen(true);
      scene.current?.setCurtain(true);
    }
  }
  function reset() {
    selectView("three-quarter");
    setCurtainOpen(false);
    scene.current?.setCurtain(false);
  }
  const ready = status === "ready";
  const description =
    views.find((item) => item.id === view)?.description ?? views[0].description;

  return (
    <section
      ref={section}
      id="the-booth"
      className={styles.section}
      aria-labelledby="booth-heading"
    >
      <header className={styles.heading}>
        <p className={styles.eyebrow}>THE BOOTH, UP CLOSE</p>
        <h2 id="booth-heading">
          Small room.
          <br />
          <em>Whole other world.</em>
        </h2>
        <p>
          Come a little closer.
          <span className={styles.wideOnly}> There’s more to it than a photograph.</span>
        </p>
      </header>
      <div className={styles.studio}>
        <div className={styles.stage}>
          <div
            ref={host}
            className={styles.canvasHost}
            role="group"
            aria-label="Interactive 3D model of the FOTOHAVN booth"
            aria-describedby="booth-instructions"
            tabIndex={ready ? 0 : -1}
            style={{ visibility: ready ? "visible" : "hidden" }}
          />
          {status !== "ready" && (
            <div className={styles.photoFallback}>
              <Image
                src={asset("booth-close")}
                alt="Photograph of the real FOTOHAVN booth at Evia, showing the cream curtain, Jacobean Walnut enclosure, photo display and print hatch."
                fill
                sizes="(max-width: 767px) 85vw, 45vw"
              />
            </div>
          )}
          <div className={styles.stageLabel} aria-hidden="true">
            FOTOHAVN{" "}
            <span>
              {status === "unavailable" ? "AT EVIA" : "IN THREE DIMENSIONS"}
            </span>
          </div>
          {ready && (
            <div className={styles.zoomControls} aria-label="Model controls">
              <button
                type="button"
                className={styles.rotateLeft}
                aria-label="Rotate booth left"
                onClick={() => scene.current?.rotate(-1)}
              >
                <span className={styles.wideOnly} aria-hidden="true">←</span>
                <ArrowUUpLeft className={styles.portraitOnly} size={24} aria-hidden="true" />
              </button>
              <span className={styles.controlDivider} aria-hidden="true" />
              <button
                type="button"
                className={styles.zoomOut}
                aria-label="Zoom out"
                onClick={() => scene.current?.zoom(-1)}
              >
                <span className={styles.wideOnly} aria-hidden="true">−</span>
                <Minus className={styles.portraitOnly} size={24} aria-hidden="true" />
              </button>
              <button
                type="button"
                className={styles.zoomIn}
                aria-label="Zoom in"
                onClick={() => scene.current?.zoom(1)}
              >
                <span className={styles.wideOnly} aria-hidden="true">+</span>
                <Plus className={styles.portraitOnly} size={24} aria-hidden="true" />
              </button>
              <button
                type="button"
                className={styles.rotateRight}
                aria-label="Rotate booth right"
                onClick={() => scene.current?.rotate(1)}
              >
                <span className={styles.wideOnly} aria-hidden="true">→</span>
                <ArrowUUpRight className={styles.portraitOnly} size={24} aria-hidden="true" />
              </button>
            </div>
          )}
          <p id="booth-instructions" className={styles.instructions}>
            {ready
              ? <>
                  <span className={styles.wideOnly}>Drag to look around. Use arrow keys when focused; + and − to zoom.</span>
                  <span className={styles.portraitOnly}>Swipe to turn.<span className={styles.srOnly}> Use arrow keys when focused; + and − to zoom.</span></span>
                </>
              : status === "unavailable"
                ? "The 3D view isn’t available on this device. Here’s the booth photographed at Evia."
                : "Preparing the 3D booth view."}
          </p>
        </div>
        <div className={styles.details}>
          <p className={styles.detailsKicker}>YOUR OWN LITTLE HAVEN</p>
          <h3>
            A little haven.
            <br />For every moment.
            <br />
            <em>Yours to keep.</em>
          </h3>
          <p className={styles.description}>{description}</p>
          <div
            className={styles.viewButtons}
            role="group"
            aria-label="Booth viewpoints"
          >
            {views.map((item) => (
              <button
                type="button"
                key={item.id}
                disabled={!ready}
                aria-pressed={view === item.id}
                onClick={() => selectView(item.id)}
              >
                {item.label}
              </button>
            ))}
          </div>
          <label className={styles.viewChooser}>
            <span className={styles.srOnly}>Booth viewpoint</span>
            <select
              value={view}
              disabled={!ready}
              onChange={(event) => {
                const next = views.find((item) => item.id === event.target.value);
                if (next) selectView(next.id);
              }}
            >
              <option value="custom" disabled>Custom view</option>
              {views.map((item) => (
                <option key={item.id} value={item.id}>{item.label}</option>
              ))}
            </select>
          </label>
          <button
            className={styles.curtainButton}
            type="button"
            disabled={!ready}
            aria-pressed={curtainOpen}
            onClick={() => {
              setCurtainOpen(!curtainOpen);
              scene.current?.setCurtain(!curtainOpen);
            }}
          >
            <span className={styles.wideOnly}>
              {curtainOpen ? "Close the curtain" : "Open the curtain"}
            </span>
            <span className={styles.portraitOnly}>
              {curtainOpen ? "Close curtain" : "Open curtain"}
            </span>
            <span className={styles.wideOnly} aria-hidden="true">{curtainOpen ? "↙" : "↗"}</span>
          </button>
          <div className={styles.secondaryControls}>
            <button type="button" disabled={!ready} onClick={reset}>
              Reset view
            </button>
          </div>
          <p className={styles.status} role="status">
            {status === "loading"
              ? "Preparing your 3D view…"
              : status === "unavailable"
                ? "Photograph view"
                : ready
                  ? `${view === "custom" ? "Custom" : views.find((item) => item.id === view)?.label} view. Curtain ${curtainOpen ? "open" : "closed"}.`
                  : ""}
          </p>
          {status === "unavailable" && (
            <button
              className="textLink"
              type="button"
              onClick={() => {
                setView("three-quarter");
                setCurtainOpen(false);
                setAttempt((value) => value + 1);
              }}
            >
              Try 3D again
            </button>
          )}
        </div>
      </div>
      <p className={styles.modelDisclaimer}>
        An illustrated view of our booth, inspired by the real thing. Interior
        details and proportions are approximate.
      </p>
    </section>
  );
}
