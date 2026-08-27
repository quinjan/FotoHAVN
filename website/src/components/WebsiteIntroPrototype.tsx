"use client";

import { usePathname, useRouter } from "next/navigation";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type RefObject,
} from "react";

import { withSiteBasePath } from "../../site.config";
import styles from "./WebsiteIntroPrototype.module.css";

type IntroPhase =
  | "idle"
  | "entering"
  | "landing"
  | "skipping"
  | "complete";
type VariantKey = "A" | "B";

type VariantAssets = {
  closed: string;
  closedMobile: string;
  open: string;
  openMobile: string;
};

const prototypeAssets: Record<VariantKey, VariantAssets> = {
  A: {
    closed: withSiteBasePath(
      "/prototype/issue-98/video-generator-pack-16x9/01-exterior-closed-1920x1080.png",
    ),
    closedMobile: withSiteBasePath(
      "/prototype/issue-98/variant-a-real-booth-closed-mobile.png",
    ),
    open: withSiteBasePath(
      "/prototype/issue-98/variant-a-real-booth-open.png",
    ),
    openMobile: withSiteBasePath(
      "/prototype/issue-98/variant-a-real-booth-open-mobile.png",
    ),
  },
  B: {
    closed: withSiteBasePath(
      "/prototype/issue-98/variant-b-drawn-booth-closed.png",
    ),
    closedMobile: withSiteBasePath(
      "/prototype/issue-98/variant-b-drawn-booth-closed-mobile.png",
    ),
    open: withSiteBasePath(
      "/prototype/issue-98/variant-b-drawn-booth-open.png",
    ),
    openMobile: withSiteBasePath(
      "/prototype/issue-98/variant-b-drawn-booth-open-mobile.png",
    ),
  },
};

const realisticCurtainAssets = {
  desktop: withSiteBasePath(
    "/prototype/issue-98/variant-a-curtain-closeup-desktop.png",
  ),
  mobile: withSiteBasePath(
    "/prototype/issue-98/variant-a-curtain-closeup-mobile.png",
  ),
};

const realisticHomepageAsset = withSiteBasePath(
  "/prototype/issue-98/variant-a-homepage-live-desktop.png",
);

const realisticJourneyVideo = withSiteBasePath(
  "/prototype/issue-98/variant-a-generated-journey-desktop.mp4",
);

const realisticDesktopVideoFallbackDuration = 5600;
const realisticDesktopLandingDuration = 1900;
const realisticMobileMotionDuration = 2800;

const variantMotionDuration: Record<VariantKey, number> = {
  A: realisticMobileMotionDuration,
  B: 2200,
};

const variantLabels: Record<VariantKey, string> = {
  A: "REALISTIC",
  B: "CANVAS",
};

const showPrototypeSwitcher = process.env.NODE_ENV !== "production";

function BoothPicture({
  className,
  desktop,
  mobile,
}: {
  className: string;
  desktop: string;
  mobile: string;
}) {
  return (
    <picture className={className}>
      <source media="(max-width: 767px)" srcSet={mobile} />
      <img src={desktop} alt="" />
    </picture>
  );
}

function VariantARealistic({
  assets,
  videoRef,
  onVideoEnded,
}: {
  assets: VariantAssets;
  videoRef: RefObject<HTMLVideoElement | null>;
  onVideoEnded: () => void;
}) {
  return (
    <>
      <div
        className={`${styles.scene} ${styles.realisticScene}`}
        aria-hidden="true"
      >
        <BoothPicture
          className={`${styles.boothFrame} ${styles.realisticZoomFrame}`}
          desktop={assets.closed}
          mobile={assets.closedMobile}
        />
      </div>
      <video
        ref={videoRef}
        className={styles.journeyVideo}
        poster={assets.closed}
        preload="auto"
        muted
        playsInline
        aria-hidden="true"
        onEnded={onVideoEnded}
      >
        <source src={realisticJourneyVideo} type="video/mp4" />
      </video>
      <BoothPicture
        className={styles.screenPortal}
        desktop={realisticHomepageAsset}
        mobile={realisticHomepageAsset}
      />
      <BoothPicture
        className={styles.curtainFoldFrame}
        desktop={realisticCurtainAssets.desktop}
        mobile={realisticCurtainAssets.mobile}
      />
      <BoothPicture
        className={styles.curtainRailFrame}
        desktop={realisticCurtainAssets.desktop}
        mobile={realisticCurtainAssets.mobile}
      />
    </>
  );
}

function VariantBCanvas({ assets }: { assets: VariantAssets }) {
  return (
    <div className={`${styles.scene} ${styles.canvasScene}`} aria-hidden="true">
      <BoothPicture
        className={`${styles.boothFrame} ${styles.openFrame}`}
        desktop={assets.open}
        mobile={assets.openMobile}
      />
      <BoothPicture
        className={`${styles.boothFrame} ${styles.closedFrame}`}
        desktop={assets.closed}
        mobile={assets.closedMobile}
      />
      <BoothPicture
        className={`${styles.boothFrame} ${styles.inkTrace}`}
        desktop={assets.closed}
        mobile={assets.closedMobile}
      />
    </div>
  );
}

function PrototypeSwitcher({
  current,
  onSelect,
}: {
  current: VariantKey;
  onSelect: (variant: VariantKey) => void;
}) {
  if (!showPrototypeSwitcher) return null;

  const alternate: VariantKey = current === "A" ? "B" : "A";

  return (
    <div className={styles.prototypeSwitcher} aria-label="Prototype variant">
      <button
        type="button"
        data-intro-control
        aria-label={`Show Variant ${alternate}`}
        onClick={() => onSelect(alternate)}
      >
        PREV
      </button>
      <p aria-live="polite">
        {current} — {variantLabels[current]}
      </p>
      <button
        type="button"
        data-intro-control
        aria-label={`Show Variant ${alternate}`}
        onClick={() => onSelect(alternate)}
      >
        NEXT
      </button>
    </div>
  );
}

/**
 * Two Website Intro Experience variants, switchable via `?variant=A|B`, on the
 * existing homepage. Reload the route to replay; no prototype state persists.
 */
export default function WebsiteIntroPrototype({
  initialVariant,
}: {
  initialVariant: VariantKey;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const [phase, setPhase] = useState<IntroPhase>("idle");
  const [variant, setVariant] = useState<VariantKey>(initialVariant);
  const introRef = useRef<HTMLElement>(null);
  const journeyVideoRef = useRef<HTMLVideoElement>(null);
  const handoffStartedRef = useRef(false);
  const completionTimerRef = useRef<number | null>(null);

  const finishIntro = useCallback(() => {
    setPhase("complete");
  }, []);

  const beginScreenHandoff = useCallback(() => {
    if (handoffStartedRef.current) return;
    handoffStartedRef.current = true;

    if (completionTimerRef.current !== null) {
      window.clearTimeout(completionTimerRef.current);
    }

    setPhase("landing");
    completionTimerRef.current = window.setTimeout(
      finishIntro,
      realisticDesktopLandingDuration,
    );
  }, [finishIntro]);

  useEffect(() => {
    if (phase !== "complete") return;

    const focusFrame = window.requestAnimationFrame(() => {
      document.getElementById("hero-heading")?.focus({ preventScroll: true });
    });

    return () => window.cancelAnimationFrame(focusFrame);
  }, [phase]);

  const startIntro = useCallback(() => {
    if (phase !== "idle") return;

    const prefersReducedMotion = window.matchMedia(
      "(prefers-reduced-motion: reduce)",
    ).matches;

    handoffStartedRef.current = false;
    setPhase("entering");
    const isRealisticDesktop =
      variant === "A" && window.matchMedia("(min-width: 768px)").matches;

    if (prefersReducedMotion) {
      completionTimerRef.current = window.setTimeout(finishIntro, 40);
      return;
    }

    if (isRealisticDesktop) {
      const video = journeyVideoRef.current;
      if (video) {
        video.currentTime = 0;
        completionTimerRef.current = window.setTimeout(
          beginScreenHandoff,
          realisticDesktopVideoFallbackDuration,
        );
        void video.play().catch(beginScreenHandoff);
        return;
      }

      beginScreenHandoff();
      return;
    }

    completionTimerRef.current = window.setTimeout(
      finishIntro,
      variantMotionDuration[variant],
    );
  }, [beginScreenHandoff, finishIntro, phase, variant]);

  const skipIntro = useCallback(() => {
    if (completionTimerRef.current !== null) {
      window.clearTimeout(completionTimerRef.current);
    }

    handoffStartedRef.current = true;
    journeyVideoRef.current?.pause();
    setPhase("skipping");
    completionTimerRef.current = window.setTimeout(finishIntro, 180);
  }, [finishIntro]);

  const selectVariant = useCallback(
    (nextVariant: VariantKey) => {
      if (completionTimerRef.current !== null) {
        window.clearTimeout(completionTimerRef.current);
      }

      handoffStartedRef.current = true;
      if (journeyVideoRef.current) {
        journeyVideoRef.current.pause();
        journeyVideoRef.current.currentTime = 0;
      }

      const params = new URLSearchParams(window.location.search);
      params.set("variant", nextVariant);

      setVariant(nextVariant);
      setPhase("idle");
      router.replace(`${pathname}?${params.toString()}`, { scroll: false });
    },
    [pathname, router],
  );

  useEffect(() => {
    const handleVariantKeys = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (
        target?.matches("input, textarea, select, [contenteditable='true']")
      ) {
        return;
      }

      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;

      event.preventDefault();
      selectVariant(variant === "A" ? "B" : "A");
    };

    document.addEventListener("keydown", handleVariantKeys);
    return () => document.removeEventListener("keydown", handleVariantKeys);
  }, [selectVariant, variant]);

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
      journeyVideoRef.current?.pause();
    },
    [],
  );

  const handleControlKeyDown = (
    event: ReactKeyboardEvent<HTMLButtonElement>,
    action: () => void,
  ) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    event.preventDefault();
    action();
  };

  if (phase === "complete") {
    return <PrototypeSwitcher current={variant} onSelect={selectVariant} />;
  }

  const className = [
    styles.intro,
    variant === "A" ? styles.variantA : styles.variantB,
    phase === "entering" || phase === "landing" ? styles.entering : "",
    phase === "landing" ? styles.landing : "",
    phase === "skipping" ? styles.skipping : "",
  ]
    .filter(Boolean)
    .join(" ");

  const assets = prototypeAssets[variant];

  return (
    <section
      ref={introRef}
      className={className}
      role="dialog"
      aria-modal="true"
      aria-labelledby="intro-title"
      aria-describedby="intro-description"
      data-variant={variant}
      data-phase={phase}
      tabIndex={-1}
    >
      <h1 id="intro-title" className={styles.screenReaderText}>
        Enter FOTOHVN
      </h1>
      <p id="intro-description" className={styles.screenReaderText}>
        {variant === "A"
          ? "Variant A. Enter the photobooth, turn toward its left-wall welcome screen, then move into the live FOTOHVN website, or skip the intro."
          : "Variant B. Zoom into the illustrated photobooth curtain, then open it from left to right to reveal the FOTOHVN website, or skip the intro."}
      </p>

      {variant === "A" ? (
        <VariantARealistic
          assets={assets}
          videoRef={journeyVideoRef}
          onVideoEnded={beginScreenHandoff}
        />
      ) : (
        <VariantBCanvas assets={assets} />
      )}
      <div className={styles.veil} aria-hidden="true" />

      <p className={styles.wordmark} aria-hidden="true">
        FOTOHVN
      </p>

      <button
        className={styles.skipButton}
        type="button"
        data-intro-control
        onClick={skipIntro}
        onKeyDown={(event) =>
          handleControlKeyDown(event, skipIntro)
        }
      >
        SKIP INTRO
      </button>

      <button
        className={styles.enterButton}
        type="button"
        data-intro-control
        disabled={phase !== "idle"}
        onClick={startIntro}
        onKeyDown={(event) =>
          handleControlKeyDown(event, startIntro)
        }
      >
        PRESS TO ENTER FOTOHVN
      </button>

      <PrototypeSwitcher current={variant} onSelect={selectVariant} />

      <p className={styles.screenReaderText} aria-live="polite">
        {phase === "entering" || phase === "landing"
          ? `Entering FOTOHVN with Variant ${variant}.`
          : ""}
      </p>
    </section>
  );
}
