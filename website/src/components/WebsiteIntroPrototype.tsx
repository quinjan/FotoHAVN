"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { withSiteBasePath } from "../../site.config";
import styles from "./WebsiteIntroPrototype.module.css";

type IntroPhase = "idle" | "entering" | "skipping" | "complete";

const desktopBooth = withSiteBasePath(
  "/prototype/issue-98/intro-booth-closed-desktop.png",
);
const mobileBooth = withSiteBasePath(
  "/prototype/issue-98/intro-booth-closed-mobile.png",
);

function BoothPicture({ className }: { className: string }) {
  return (
    <picture className={className}>
      <source media="(max-width: 767px)" srcSet={mobileBooth} />
      <img src={desktopBooth} alt="" />
    </picture>
  );
}

/**
 * One selected Website Intro Experience prototype on the existing homepage.
 * Reload the route to replay it; the component intentionally stores no state.
 */
export default function WebsiteIntroPrototype() {
  const [phase, setPhase] = useState<IntroPhase>("idle");
  const introRef = useRef<HTMLElement>(null);
  const completionTimerRef = useRef<number | null>(null);

  const finishIntro = useCallback(() => {
    setPhase("complete");
    requestAnimationFrame(() => {
      document.getElementById("hero-heading")?.focus({ preventScroll: true });
    });
  }, []);

  const startIntro = useCallback(() => {
    if (phase !== "idle") return;

    const prefersReducedMotion = window.matchMedia(
      "(prefers-reduced-motion: reduce)",
    ).matches;

    setPhase("entering");
    completionTimerRef.current = window.setTimeout(
      finishIntro,
      prefersReducedMotion ? 40 : 820,
    );
  }, [finishIntro, phase]);

  const skipIntro = useCallback(() => {
    if (completionTimerRef.current !== null) {
      window.clearTimeout(completionTimerRef.current);
    }

    setPhase("skipping");
    completionTimerRef.current = window.setTimeout(finishIntro, 180);
  }, [finishIntro]);

  useEffect(() => {
    if (phase === "complete") return;

    const siteContent = document.getElementById("site-content");
    const previousOverflow = document.body.style.overflow;

    siteContent?.setAttribute("inert", "");
    document.body.style.overflow = "hidden";
    introRef.current?.focus({ preventScroll: true });

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        skipIntro();
        return;
      }

      if (event.key !== "Tab") return;

      const controls = Array.from(
        document.querySelectorAll<HTMLButtonElement>(
          "[data-intro-control]:not([disabled])",
        ),
      );
      if (controls.length === 0) return;

      const currentIndex = controls.indexOf(
        document.activeElement as HTMLButtonElement,
      );
      const step = event.shiftKey ? -1 : 1;
      const nextIndex =
        currentIndex === -1
          ? 0
          : (currentIndex + step + controls.length) % controls.length;

      event.preventDefault();
      controls[nextIndex]?.focus();
    };

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      siteContent?.removeAttribute("inert");
      document.body.style.overflow = previousOverflow;
    };
  }, [phase, skipIntro]);

  useEffect(
    () => () => {
      if (completionTimerRef.current !== null) {
        window.clearTimeout(completionTimerRef.current);
      }
    },
    [],
  );

  if (phase === "complete") return null;

  const className = [
    styles.intro,
    phase === "entering" ? styles.entering : "",
    phase === "skipping" ? styles.skipping : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <section
      ref={introRef}
      className={className}
      role="dialog"
      aria-modal="true"
      aria-labelledby="intro-title"
      aria-describedby="intro-description"
      tabIndex={-1}
    >
      <h1 id="intro-title" className={styles.screenReaderText}>
        Enter FOTOHVN
      </h1>
      <p id="intro-description" className={styles.screenReaderText}>
        Open the photobooth curtain to enter the FOTOHVN website, or skip the
        intro.
      </p>

      <div className={styles.curtainStage} aria-hidden="true">
        <BoothPicture className={`${styles.curtainHalf} ${styles.curtainLeft}`} />
        <BoothPicture className={`${styles.curtainHalf} ${styles.curtainRight}`} />
      </div>
      <div className={styles.vignette} aria-hidden="true" />

      <p className={styles.wordmark} aria-hidden="true">
        FOTOHVN
      </p>

      <button
        className={styles.skipButton}
        type="button"
        data-intro-control
        onClick={skipIntro}
        onKeyDown={(event) => {
          if (event.key !== "Enter" && event.key !== " ") return;
          event.preventDefault();
          skipIntro();
        }}
      >
        SKIP INTRO
      </button>

      <button
        className={styles.enterButton}
        type="button"
        data-intro-control
        disabled={phase !== "idle"}
        onClick={startIntro}
        onKeyDown={(event) => {
          if (event.key !== "Enter" && event.key !== " ") return;
          event.preventDefault();
          startIntro();
        }}
      >
        PRESS TO ENTER FOTOHVN
      </button>

      <p className={styles.screenReaderText} aria-live="polite">
        {phase === "entering" ? "Entering FOTOHVN." : ""}
      </p>
    </section>
  );
}
