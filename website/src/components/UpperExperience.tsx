"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import type { KeyboardEvent } from "react";

import styles from "./UpperExperience.module.css";

const experienceFeatures = [
  {
    id: "enclosed",
    title: "ENCLOSED",
    description: "A private little space made for candid moments.",
    image: "/images/experience-enclosed.png",
    alt: "Guests sharing a candid moment inside the enclosed FOTOHVN booth.",
    mediaClass: styles.featureMediaPortrait,
  },
  {
    id: "printed",
    title: "PRINTED",
    description: "Take home photographs, not just digital files.",
    image: "/images/experience-printed.png",
    alt: "Fresh FOTOHVN photo strips held against a warm paper surface.",
    mediaClass: styles.featureMediaLandscape,
  },
  {
    id: "distinctive",
    title: "DISTINCTIVE",
    description: "Choose from FOTOHVN's specialized photographic looks.",
    image: "/images/experience-distinctive.png",
    alt: "A tactile detail of the FOTOHVN booth and its photographic finish.",
    mediaClass: styles.featureMediaNarrow,
  },
] as const;

const photographicLooks = [
  {
    id: "classic",
    name: "CLASSIC",
    description: "Clean, timeless tones with subtle analog character.",
    image: "/images/look-classic.png",
    alt: "A FOTOHVN portrait rendered in clean, timeless Classic tones.",
  },
  {
    id: "vintage",
    name: "VINTAGE",
    description: "Warm, faded tones inspired by old photographs.",
    image: "/images/look-vintage.png",
    alt: "A FOTOHVN portrait rendered in warm, softly faded Vintage tones.",
  },
  {
    id: "monochrome",
    name: "MONOCHROME",
    description: "Rich black-and-white with a classic studio feel.",
    image: "/images/look-monochrome.png",
    alt: "A FOTOHVN portrait rendered in rich studio black-and-white.",
  },
  {
    id: "signature",
    name: "FOTOHVN SIGNATURE",
    description:
      "A distinctive FOTOHVN house look developed specifically for the brand.",
    image: "/images/look-signature.png",
    alt: "A portrait rendered in the distinctive FOTOHVN Signature house look.",
  },
] as const;

type LookId = (typeof photographicLooks)[number]["id"];

export default function UpperExperience() {
  const [activeLookId, setActiveLookId] = useState<LookId>("classic");
  const tabRefs = useRef<Array<HTMLButtonElement | null>>([]);

  const activeLook =
    photographicLooks.find((look) => look.id === activeLookId) ??
    photographicLooks[0];

  useEffect(() => {
    const syncLookFromHash = () => {
      const hash = window.location.hash.slice(1);
      const matchingLook = photographicLooks.find(
        (look) => hash === `look-${look.id}`,
      );

      if (matchingLook) {
        setActiveLookId(matchingLook.id);
      }
    };

    syncLookFromHash();
    window.addEventListener("hashchange", syncLookFromHash);

    return () => window.removeEventListener("hashchange", syncLookFromHash);
  }, []);

  const activateLook = (index: number, moveFocus = false) => {
    const nextLook = photographicLooks[index];

    setActiveLookId(nextLook.id);
    window.history.replaceState(null, "", `#look-${nextLook.id}`);

    if (moveFocus) {
      tabRefs.current[index]?.focus();
    }
  };

  const handleTabKeyDown = (
    event: KeyboardEvent<HTMLButtonElement>,
    currentIndex: number,
  ) => {
    let nextIndex: number | undefined;

    switch (event.key) {
      case "ArrowRight":
      case "ArrowDown":
        nextIndex = (currentIndex + 1) % photographicLooks.length;
        break;
      case "ArrowLeft":
      case "ArrowUp":
        nextIndex =
          (currentIndex - 1 + photographicLooks.length) %
          photographicLooks.length;
        break;
      case "Home":
        nextIndex = 0;
        break;
      case "End":
        nextIndex = photographicLooks.length - 1;
        break;
      default:
        return;
    }

    event.preventDefault();
    activateLook(nextIndex, true);
  };

  return (
    <div className={styles.upperExperience}>
      <section className={styles.hero} aria-labelledby="hero-heading">
        <Image
          className={styles.heroImage}
          src="/images/hero-booth.png"
          alt="The enclosed FOTOHVN booth arranged in an intimate celebration setting."
          fill
          preload
          loading="eager"
          sizes="100vw"
        />
        <div className={styles.heroVeil} aria-hidden="true" />

        <div className={`${styles.container} ${styles.heroContent}`}>
          <p className={styles.heroBrand}>FOTOHVN</p>
          <h1 id="hero-heading" className={styles.heroHeading}>
            <span>PHOTOGRAPHS,</span>
            <span>DEVELOPED DIFFERENTLY.</span>
          </h1>
          <p className={styles.heroSupportingText}>
            An enclosed vintage photobooth experience for celebrations worth
            remembering.
          </p>
          <div className={styles.heroActions}>
            <a className={styles.primaryButton} href="#inquire">
              BOOK FOTOHVN
            </a>
            <a className={styles.editorialLink} href="#experience">
              EXPLORE THE EXPERIENCE
            </a>
          </div>
        </div>
      </section>

      <section
        id="experience"
        className={styles.experience}
        aria-labelledby="experience-heading"
      >
        <div className={styles.container}>
          <header className={styles.experienceHeader}>
            <h2 id="experience-heading" className={styles.sectionHeading}>
              THE FOTOHVN EXPERIENCE
            </h2>
            <p className={styles.sectionIntro}>
              A little room for photographs, laughter, and moments you&apos;ll
              want to keep.
            </p>
          </header>

          <div className={styles.featureGrid}>
            {experienceFeatures.map((feature, index) => (
              <article className={styles.feature} key={feature.id}>
                <div className={styles.featureMediaStage}>
                  <div
                    className={`${styles.featureMedia} ${feature.mediaClass}`}
                  >
                    <Image
                      className={styles.coverImage}
                      src={feature.image}
                      alt={feature.alt}
                      fill
                      sizes="(max-width: 767px) calc(100vw - 48px), (max-width: 1199px) 29vw, 384px"
                    />
                  </div>
                </div>
                <div className={styles.featureCopy}>
                  <p className={styles.featureNumber} aria-hidden="true">
                    0{index + 1}
                  </p>
                  <h3>{feature.title}</h3>
                  <p>{feature.description}</p>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section
        id="photographic-looks"
        className={styles.looks}
        aria-labelledby="looks-heading"
      >
        <div className={styles.container}>
          <header className={styles.looksHeader}>
            <p className={styles.eyebrow}>FOTOHVN PHOTOGRAPHIC LOOKS</p>
            <h2 id="looks-heading" className={styles.sectionHeading}>
              CHOOSE YOUR LOOK
            </h2>
            <p className={styles.looksSubheading}>
              One booth. Four ways to remember it.
            </p>
          </header>

          <div className={styles.looksGrid}>
            <div
              id="look-panel"
              className={styles.lookPanel}
              role="tabpanel"
              aria-labelledby={`look-${activeLook.id}`}
              tabIndex={0}
            >
              <div className={styles.lookMedia}>
                {photographicLooks.map((look) => {
                  const isActive = look.id === activeLook.id;

                  return (
                    <div
                      className={`${styles.lookImageLayer} ${
                        isActive ? styles.lookImageLayerActive : ""
                      }`}
                      aria-hidden={!isActive}
                      key={look.id}
                    >
                      <Image
                        className={styles.coverImage}
                        src={look.image}
                        alt={isActive ? look.alt : ""}
                        fill
                        sizes="(max-width: 767px) calc(100vw - 48px), (max-width: 1199px) 52vw, 720px"
                      />
                    </div>
                  );
                })}
              </div>
              <p className={styles.lookCaption} aria-live="polite">
                <span>{activeLook.name}</span>
                {activeLook.description}
              </p>
            </div>

            <div
              className={styles.lookTabs}
              role="tablist"
              aria-label="FOTOHVN photographic looks"
              aria-orientation="vertical"
            >
              {photographicLooks.map((look, index) => {
                const isActive = look.id === activeLook.id;

                return (
                  <button
                    id={`look-${look.id}`}
                    className={`${styles.lookTab} ${
                      isActive ? styles.lookTabActive : ""
                    }`}
                    type="button"
                    role="tab"
                    aria-selected={isActive}
                    aria-controls="look-panel"
                    tabIndex={isActive ? 0 : -1}
                    onClick={() => activateLook(index)}
                    onKeyDown={(event) => handleTabKeyDown(event, index)}
                    ref={(node) => {
                      tabRefs.current[index] = node;
                    }}
                    key={look.id}
                  >
                    <span className={styles.lookTabNumber} aria-hidden="true">
                      0{index + 1}
                    </span>
                    <span className={styles.lookTabCopy}>
                      <span className={styles.lookTabName}>{look.name}</span>
                      <span className={styles.lookTabDescription}>
                        {look.description}
                      </span>
                    </span>
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
