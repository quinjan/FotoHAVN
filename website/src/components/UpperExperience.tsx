"use client";

import Image from "next/image";
import { useRef, useState } from "react";
import type { KeyboardEvent } from "react";

import { withSiteBasePath } from "../../site.config";
import styles from "./UpperExperience.module.css";
import heroBoothImage from "../../public/images/hero-booth.png";

const accordionItems = [
  {
    id: "enclosed",
    title: "ENCLOSED",
    description:
      "Draw the curtain and share a private moment in a little room of your own.",
    image: withSiteBasePath("/images/experience-enclosed.png"),
    alt: "An editorial view inside the enclosed FOTOHVN booth.",
  },
  {
    id: "together",
    title: "TOGETHER",
    description:
      "Take a little time to laugh, experiment, and make a photograph together.",
    image: withSiteBasePath("/images/candid-guests.png"),
    alt: "Two people sharing a playful moment inside the booth.",
  },
  {
    id: "printed",
    title: "PRINTED",
    description:
      "Leave with a physical keepsake: a Photo Strip or print made to be held.",
    image: withSiteBasePath("/images/printed-strips.png"),
    alt: "Printed FOTOHVN Photo Strips resting on a warm paper surface.",
  },
] as const;

const marqueeTerms = [
  "ENCLOSED",
  "PRINTED",
  "PRIVATE MOMENT",
  "PHYSICAL KEEPSAKE",
  "MALL BOOTH",
  "EVENT RENTAL",
  "PHOTO STRIP",
  "FOTOHVN",
] as const;

type AccordionId = (typeof accordionItems)[number]["id"];

export default function UpperExperience() {
  const [activeAccordion, setActiveAccordion] =
    useState<AccordionId>("enclosed");
  const [isMarqueePaused, setIsMarqueePaused] = useState(false);
  const triggerRefs = useRef<Array<HTMLButtonElement | null>>([]);

  const handleAccordionKeyDown = (
    event: KeyboardEvent<HTMLButtonElement>,
    currentIndex: number,
  ) => {
    let nextIndex: number | undefined;

    switch (event.key) {
      case "Enter":
      case " ":
      case "Spacebar":
        event.preventDefault();
        setActiveAccordion(accordionItems[currentIndex].id);
        return;
      case "ArrowRight":
      case "ArrowDown":
        nextIndex = (currentIndex + 1) % accordionItems.length;
        break;
      case "ArrowLeft":
      case "ArrowUp":
        nextIndex =
          (currentIndex - 1 + accordionItems.length) % accordionItems.length;
        break;
      case "Home":
        nextIndex = 0;
        break;
      case "End":
        nextIndex = accordionItems.length - 1;
        break;
      default:
        return;
    }

    event.preventDefault();
    triggerRefs.current[nextIndex]?.focus();
  };

  return (
    <div className={styles.upperExperience}>
      <section className={styles.hero} aria-labelledby="hero-heading">
        <Image
          className={styles.heroImage}
          src={heroBoothImage}
          alt="The vintage-style enclosed FOTOHVN booth in a warm, tactile setting."
          fill
          loading="eager"
          sizes="100vw"
        />
        <div className={styles.heroVeil} aria-hidden="true" />

        <div className={styles.container + " " + styles.heroContent}>
          <h1 id="hero-heading" className={styles.heroHeading} tabIndex={-1}>
            <span>PHOTOGRAPHS,</span>
            <span>DEVELOPED DIFFERENTLY.</span>
          </h1>
          <p className={styles.heroSupportingText}>
            An enclosed vintage photobooth experience for celebrations worth
            remembering.
          </p>
          <div className={styles.heroActions}>
            <a className={styles.primaryButton} href="#find-a-booth">
              FIND A BOOTH
            </a>
            <a className={styles.secondaryButton} href="#rent-fotohavn">
              RENT FOTOHVN
            </a>
          </div>
        </div>

        <figure className={styles.heroPrint}>
          <Image
            className={styles.heroPrintImage}
            src={withSiteBasePath("/images/printed-strips.png")}
            alt="A set of printed FOTOHVN Photo Strips."
            fill
            sizes="(max-width: 767px) 120px, 180px"
          />
        </figure>
      </section>

      <section
        id="experience"
        className={styles.bentoSection}
        aria-label="The FOTOHVN experience"
      >
        <div className={styles.container}>
          <div className={styles.bentoGrid}>
            <article className={styles.bentoCard + " " + styles.bentoPrimary}>
              <Image
                className={styles.cardImage}
                src={withSiteBasePath("/images/experience-enclosed.png")}
                alt="An editorial view of the enclosed booth interior."
                fill
                sizes="(max-width: 767px) calc(100vw - 48px), (max-width: 1023px) calc(100vw - 96px), 58vw"
              />
              <div className={styles.cardVeil} aria-hidden="true" />
              <div className={styles.cardCopy}>
                <h2>A LITTLE ROOM FOR REAL MOMENTS</h2>
                <p>
                  Draw the curtain and share a little room away from the crowd.
                </p>
              </div>
            </article>

            <a
              className={
                styles.bentoCard +
                " " +
                styles.bentoSecondary +
                " " +
                styles.clickableCard
              }
              href="#find-a-booth"
            >
              <Image
                className={styles.cardImage}
                src={withSiteBasePath("/images/hero-booth.png")}
                alt="The FOTOHVN enclosed booth."
                fill
                sizes="(max-width: 767px) calc(100vw - 48px), (max-width: 1023px) 45vw, 42vw"
              />
              <div className={styles.cardVeil} aria-hidden="true" />
              <div className={styles.cardCopy}>
                <h3>FIND A BOOTH</h3>
                <p>
                  Mall use is pay-per-use. Ask for the current location and visit
                  details.
                </p>
              </div>
            </a>

            <a
              className={
                styles.bentoCard +
                " " +
                styles.bentoTertiary +
                " " +
                styles.clickableCard
              }
              href="#rent-fotohavn"
            >
              <Image
                className={styles.cardImage}
                src={withSiteBasePath("/images/printed-strips.png")}
                alt="Printed FOTOHVN Photo Strips."
                fill
                sizes="(max-width: 767px) calc(100vw - 48px), (max-width: 1023px) 45vw, 42vw"
              />
              <div className={styles.cardVeil} aria-hidden="true" />
              <div className={styles.cardCopy}>
                <h3>RENT FOTOHVN</h3>
                <p>
                  Event rental is billed by the hour with unlimited prints. Ask
                  about your date.
                </p>
              </div>
            </a>
          </div>
        </div>
      </section>

      <section
        id="the-booth"
        className={styles.accordionSection}
        aria-labelledby="accordion-heading"
      >
        <div className={styles.container}>
          <header className={styles.sectionHeader}>
            <p className={styles.sectionLabel}>THE BOOTH</p>
            <h2 id="accordion-heading">ENCLOSED, TOGETHER, PRINTED.</h2>
          </header>

          <div className={styles.accordion}>
            {accordionItems.map((item, index) => {
              const isActive = activeAccordion === item.id;

              return (
                <article
                  className={styles.accordionItem}
                  data-active={isActive}
                  key={item.id}
                >
                  <div className={styles.accordionMedia}>
                    <Image
                      className={styles.cardImage}
                      src={item.image}
                      alt={item.alt}
                      fill
                      sizes="(max-width: 767px) calc(100vw - 48px), (max-width: 1023px) calc(100vw - 96px), 55vw"
                    />
                    <div className={styles.accordionVeil} aria-hidden="true" />
                  </div>
                  <button
                    ref={(node) => {
                      triggerRefs.current[index] = node;
                    }}
                    className={styles.accordionTrigger}
                    type="button"
                    aria-expanded={isActive}
                    aria-controls={"accordion-panel-" + item.id}
                    onClick={() => setActiveAccordion(item.id)}
                    onKeyDown={(event) =>
                      handleAccordionKeyDown(event, index)
                    }
                  >
                    <span>{item.title}</span>
                  </button>
                  <div
                    id={"accordion-panel-" + item.id}
                    className={styles.accordionPanel}
                    hidden={!isActive}
                  >
                    <p>{item.description}</p>
                  </div>
                </article>
              );
            })}
          </div>
        </div>
      </section>

      <section
        className={styles.marqueeSection}
        aria-label="FOTOHVN experience words"
        data-paused={isMarqueePaused}
      >
        <p className={styles.screenReaderText}>
          FOTOHVN is an enclosed mall booth and event rental experience centered
          on private moments, physical keepsakes, and printed Photo Strips.
        </p>
        <div className={styles.marqueeViewport}>
          <div
            className={styles.marqueeTrack + " " + styles.marqueeTrackForward}
            aria-hidden="true"
          >
            {[...marqueeTerms, ...marqueeTerms].map((term, index) => (
              <span key={"forward-" + term + "-" + index}>{term}</span>
            ))}
          </div>
          <div
            className={styles.marqueeTrack + " " + styles.marqueeTrackReverse}
            aria-hidden="true"
          >
            {[...marqueeTerms]
              .reverse()
              .concat([...marqueeTerms].reverse())
              .map((term, index) => (
                <span key={"reverse-" + term + "-" + index}>{term}</span>
              ))}
          </div>
          <div className={styles.reducedMotionTerms} aria-hidden="true">
            {marqueeTerms.map((term) => (
              <span key={term}>{term}</span>
            ))}
          </div>
        </div>
        <div className={styles.container + " " + styles.marqueeControlRow}>
          <button
            className={styles.motionControl}
            type="button"
            aria-pressed={isMarqueePaused}
            onClick={() => setIsMarqueePaused((isPaused) => !isPaused)}
          >
            {isMarqueePaused ? "PLAY MOTION" : "PAUSE MOTION"}
          </button>
        </div>
      </section>
    </div>
  );
}
