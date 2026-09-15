"use client";

import { useState } from "react";
import { ArrowDownIcon } from "@phosphor-icons/react/dist/csr/ArrowDown";
import PreparedPhoto from "./EventPhoto";
import { rentFotohavnSectionHash, withSiteBasePath } from "../../site.config";
import styles from "./HeroExterior.module.css";
import photoSizes from "../../event-photo-sizes.json";

const portraitSourceSet = [...photoSizes.imageSizes, ...photoSizes.deviceSizes]
  .map(width => `${withSiteBasePath(`/images/hero/exterior-portrait-${width}w.webp`)} ${width}w`).join(", ");

export default function HeroExterior() {
  const [imageFailed, setImageFailed] = useState(false);
  return (
    <section className={styles.hero} aria-labelledby="hero-heading"
      data-hero-exterior data-image-failed={imageFailed}>
      <div className={styles.scene}>
        <picture>
          <source media="(max-width: 767px), (max-width: 1100px) and (orientation: portrait)"
            srcSet={portraitSourceSet} sizes="100vw" />
        <PreparedPhoto
          src={withSiteBasePath("/images/hero/exterior.webp")}
          alt="An illustrated FOTOHAVN booth with a glowing PHOTOBOOTH sign, walnut frame and closed cream curtain."
          fill sizes="100vw" loading="eager" fetchPriority="high"
          className={styles.exterior}
          onError={() => setImageFailed(true)}
          onLoad={() => setImageFailed(false)}
        />
        </picture>
      </div>
      <div className={styles.copy} data-hero-copy>
        <p className={styles.eyebrow}>A LITTLE ROOM. A LASTING FEELING.</p>
        <h1 id="hero-heading" tabIndex={-1}>
          <span>Photographs,</span><em>developed differently.</em>
        </h1>
        <p className={styles.description}>
          An enclosed vintage photobooth experience for celebrations worth remembering.
        </p>
        <div className={styles.actions}>
          <a
            className="button"
            href={rentFotohavnSectionHash}
          >
            RENT FOTOHAVN <ArrowDownIcon size={20} weight="light" aria-hidden="true" />
          </a>
          <a className={styles.explore} href="#experience">
            <span>EXPLORE THE EXPERIENCE</span>
            <ArrowDownIcon size={20} weight="light" aria-hidden="true" />
          </a>
        </div>
        {imageFailed && <p className={styles.status} role="status">
          The illustration couldn’t load. The experience continues below.
        </p>}
      </div>
    </section>
  );
}
