"use client";

import { useGSAP } from "@gsap/react";
import gsap from "gsap";
import { ScrollTrigger } from "gsap/ScrollTrigger";
import Image from "next/image";
import { useRef, useState } from "react";
import type { CSSProperties, KeyboardEvent } from "react";

import { withSiteBasePath } from "../../site.config";
import styles from "./MiddleExperience.module.css";

gsap.registerPlugin(useGSAP, ScrollTrigger);

const galleryStories = [
  {
    label: "THE ENCLOSURE",
    src: withSiteBasePath("/images/hero-booth.png"),
    alt: "The vintage-style enclosed FOTOHVN booth.",
  },
  {
    label: "A MOMENT TOGETHER",
    src: withSiteBasePath("/images/candid-guests.png"),
    alt: "Two people sharing a playful moment inside the booth.",
  },
  {
    label: "THE PHYSICAL PRINT",
    src: withSiteBasePath("/images/printed-strips.png"),
    alt: "Printed FOTOHVN Photo Strips on a tactile surface.",
  },
  {
    label: "THE BOOTH DETAIL",
    src: withSiteBasePath("/images/booth-detail.png"),
    alt: "A close view of the booth curtain and crafted details.",
  },
] as const;

const stackCards = [
  {
    title: "STEP INSIDE",
    copy: "A private enclosure makes a little space away from the room around you.",
    src: withSiteBasePath("/images/experience-enclosed.png"),
    alt: "An editorial view inside the enclosed booth.",
  },
  {
    title: "BE YOURSELVES",
    copy: "Share a moment, laugh, experiment, and make a photograph together.",
    src: withSiteBasePath("/images/candid-guests.png"),
    alt: "Two people making a photograph together inside the booth.",
  },
  {
    title: "KEEP THE PHOTOGRAPH",
    copy: "Leave with a physical Photo Strip or print made to be held.",
    src: withSiteBasePath("/images/experience-printed.png"),
    alt: "A physical FOTOHVN print arranged on warm paper.",
  },
] as const;

const notes = [
  {
    quote:
      "A little room for photographs, laughter, and moments you'll want to keep.",
    primary: withSiteBasePath("/images/candid-guests.png"),
    secondary: withSiteBasePath("/images/booth-detail.png"),
    primaryAlt: "Two people sharing a moment inside the booth.",
    secondaryAlt: "A close crop of the FOTOHVN booth curtain.",
  },
  {
    quote:
      "Step inside, draw the curtain, and take a little time to laugh, experiment, and make something together.",
    primary: withSiteBasePath("/images/experience-enclosed.png"),
    secondary: withSiteBasePath("/images/candid-guests.png"),
    primaryAlt: "An editorial view inside the enclosed booth.",
    secondaryAlt: "Two people making a photograph together.",
  },
  {
    quote:
      "A private, tactile photography experience with something physical to keep.",
    primary: withSiteBasePath("/images/printed-strips.png"),
    secondary: withSiteBasePath("/images/experience-printed.png"),
    primaryAlt: "Printed FOTOHVN Photo Strips.",
    secondaryAlt: "A physical FOTOHVN print on a paper surface.",
  },
] as const;

export default function MiddleExperience() {
  const rootRef = useRef<HTMLDivElement>(null);
  const gallerySectionRef = useRef<HTMLElement>(null);
  const galleryHeadingRef = useRef<HTMLDivElement>(null);
  const [activeNote, setActiveNote] = useState(0);

  useGSAP(
    () => {
      let mediaQueries: gsap.MatchMedia | undefined;
      const animationContext = gsap.context(() => {
        mediaQueries = gsap.matchMedia();
        mediaQueries.add(
          "(min-width: 1024px) and (prefers-reduced-motion: no-preference)",
          () => {
            if (gallerySectionRef.current && galleryHeadingRef.current) {
              ScrollTrigger.create({
                trigger: gallerySectionRef.current,
                pin: galleryHeadingRef.current,
                start: "top top+=96",
                end: "bottom bottom-=96",
                pinSpacing: false,
              });
            }

            gsap.utils
              .toArray<HTMLElement>("[data-gallery-story]", rootRef.current)
              .forEach((story) => {
                const media = story.querySelector<HTMLElement>(
                  "[data-gallery-media]",
                );
                const overlay = story.querySelector<HTMLElement>(
                  "[data-gallery-overlay]",
                );

                if (!media || !overlay) return;

                gsap
                  .timeline({
                    scrollTrigger: {
                      trigger: story,
                      start: "top bottom-=96",
                      end: "bottom top+=96",
                      scrub: true,
                    },
                  })
                  .fromTo(
                    media,
                    { scale: 0.8, opacity: 0.2 },
                    {
                      scale: 1,
                      opacity: 1,
                      duration: 0.5,
                      ease: "none",
                    },
                  )
                  .to(media, {
                    opacity: 0.2,
                    duration: 0.5,
                    ease: "none",
                  })
                  .to(
                    overlay,
                    { opacity: 0.32, duration: 0.5, ease: "none" },
                    "<",
                  );
              });

            gsap.utils
              .toArray<HTMLElement>("[data-stack-card]", rootRef.current)
              .forEach((card) => {
                gsap.fromTo(
                  card,
                  { scale: 0.94, yPercent: 18 },
                  {
                    scale: 1,
                    yPercent: 0,
                    ease: "none",
                    scrollTrigger: {
                      trigger: card,
                      start: "top bottom",
                      end: "top top+=240",
                      scrub: true,
                    },
                  },
                );
              });
          },
        );
      }, rootRef);

      return () => {
        mediaQueries?.revert();
        animationContext.revert();
      };
    },
    { scope: rootRef },
  );

  const cycleNote = (direction: number) => {
    setActiveNote(
      (current) => (current + direction + notes.length) % notes.length,
    );
  };

  const handleCarouselKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "ArrowLeft") {
      event.preventDefault();
      cycleNote(-1);
    }

    if (event.key === "ArrowRight") {
      event.preventDefault();
      cycleNote(1);
    }
  };

  const currentNote = notes[activeNote];

  return (
    <div ref={rootRef} className={styles.middleExperience}>
      <section
        ref={gallerySectionRef}
        className={styles.gallerySection}
        aria-labelledby="gallery-heading"
      >
        <div className={styles.container + " " + styles.galleryGrid}>
          <div ref={galleryHeadingRef} className={styles.galleryHeading}>
            <p className={styles.sectionLabel}>THE BOOTH</p>
            <h2 id="gallery-heading">THE FOTOHVN EXPERIENCE</h2>
            <p>Step inside together. Leave with something real.</p>
          </div>

          <div className={styles.galleryStories}>
            {galleryStories.map((story) => (
              <figure
                className={styles.galleryStory}
                data-gallery-story
                key={story.label}
              >
                <div className={styles.galleryMedia} data-gallery-media>
                  <Image
                    className={styles.image}
                    src={story.src}
                    alt={story.alt}
                    fill
                    sizes="(max-width: 1023px) calc(100vw - 96px), 65vw"
                  />
                  <div
                    className={styles.galleryOverlay}
                    data-gallery-overlay
                    aria-hidden="true"
                  />
                </div>
                <figcaption>{story.label}</figcaption>
              </figure>
            ))}
          </div>
        </div>
      </section>

      <section
        id="prints"
        className={styles.stackSection}
        aria-labelledby="stack-heading"
      >
        <div className={styles.container}>
          <header className={styles.stackHeader}>
            <p className={styles.sectionLabel}>PHYSICAL PRINTS</p>
            <h2 id="stack-heading">
              STEP INSIDE. BE YOURSELVES. KEEP THE PHOTOGRAPH.
            </h2>
          </header>

          <div className={styles.stackCards}>
            {stackCards.map((card, index) => (
              <article
                className={styles.stackCard}
                data-stack-card
                key={card.title}
                style={{ "--stack-index": index } as CSSProperties}
              >
                <div className={styles.stackCopy}>
                  <h3>{card.title}</h3>
                  <p>{card.copy}</p>
                </div>
                <div className={styles.stackMedia}>
                  <Image
                    className={styles.image}
                    src={card.src}
                    alt={card.alt}
                    fill
                    loading={
                      index === stackCards.length - 1 ? "eager" : undefined
                    }
                    sizes="(max-width: 1023px) calc(100vw - 96px), 58vw"
                  />
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section
        className={styles.notesSection}
        aria-labelledby="notes-heading"
      >
        <div className={styles.container}>
          <header className={styles.notesHeader}>
            <p className={styles.sectionLabel}>FOTOHVN</p>
            <h2 id="notes-heading">NOTES FROM INSIDE THE BOOTH</h2>
          </header>

          <div
            className={styles.carousel}
            role="region"
            aria-roledescription="carousel"
            aria-label="FOTOHVN editorial notes"
            tabIndex={0}
            onKeyDown={handleCarouselKeyDown}
          >
            <div className={styles.portraitComposition}>
              <div className={styles.portraitPrimary}>
                <Image
                  className={styles.image}
                  src={currentNote.primary}
                  alt={currentNote.primaryAlt}
                  fill
                  sizes="(max-width: 767px) 64vw, 34vw"
                />
              </div>
              <div className={styles.portraitSecondary}>
                <Image
                  className={styles.image}
                  src={currentNote.secondary}
                  alt={currentNote.secondaryAlt}
                  fill
                  sizes="(max-width: 767px) 42vw, 20vw"
                />
              </div>
            </div>

            <div className={styles.noteCopy}>
              <p className={styles.noteAttribution}>FOTOHVN</p>
              <blockquote>&ldquo;{currentNote.quote}&rdquo;</blockquote>
              <p className={styles.screenReaderStatus} aria-live="polite">
                Note {activeNote + 1} of {notes.length}: {currentNote.quote},
                attributed to FOTOHVN.
              </p>
              <div className={styles.carouselControls}>
                <button type="button" onClick={() => cycleNote(-1)}>
                  <span aria-hidden="true">←</span> PREVIOUS
                </button>
                <button type="button" onClick={() => cycleNote(1)}>
                  NEXT <span aria-hidden="true">→</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
