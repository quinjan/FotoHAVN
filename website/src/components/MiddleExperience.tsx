import Image from "next/image";

import styles from "./MiddleExperience.module.css";

const inclusions = [
  "3 hours of unlimited booth sessions",
  "Printed photo strips",
  "Digital copies",
  "Event attendant",
  "Custom event photo template",
  "Specialized FOTOHVN photographic looks",
  "Setup & teardown",
];

const galleryImages = [
  {
    src: "/images/hero-booth.png",
    alt: "The enclosed FOTOHVN booth set within a warmly lit celebration",
    caption: "A little room, ready for the evening.",
    className: styles.galleryAnchor,
    sizes: "(max-width: 767px) 100vw, 66vw",
  },
  {
    src: "/images/candid-guests.png",
    alt: "Guests sharing a candid moment inside the FOTOHVN booth",
    caption: "Inside the curtain.",
    className: styles.galleryPortrait,
    sizes: "(max-width: 767px) 100vw, 32vw",
  },
  {
    src: "/images/printed-strips.png",
    alt: "Printed FOTOHVN photo strips resting on a tactile surface",
    caption: "Photographs made to be held.",
    className: styles.galleryStrip,
    sizes: "(max-width: 767px) 100vw, 24vw",
  },
  {
    src: "/images/booth-detail.png",
    alt: "Close view of the FOTOHVN booth curtain and crafted details",
    caption: "Every detail, considered.",
    className: styles.galleryDetail,
    sizes: "(max-width: 767px) 100vw, 40vw",
  },
  {
    src: "/images/look-signature.png",
    alt: "A guest portrait developed in the FOTOHVN Signature look",
    caption: "The FOTOHVN Signature look.",
    className: styles.gallerySignature,
    sizes: "(max-width: 767px) 100vw, 24vw",
  },
];

const events = [
  {
    title: "WEDDINGS",
    src: "/images/candid-guests.png",
    alt: "Wedding guests making a photograph together inside FOTOHVN",
  },
  {
    title: "DEBUTS",
    src: "/images/look-vintage.png",
    alt: "A debut portrait developed with FOTOHVN's warm vintage look",
  },
  {
    title: "BIRTHDAYS",
    src: "/images/printed-strips.png",
    alt: "FOTOHVN photo strips made during a birthday celebration",
  },
  {
    title: "CORPORATE EVENTS",
    src: "/images/hero-booth.png",
    alt: "The FOTOHVN booth installed for an elegant corporate event",
  },
  {
    title: "PRIVATE CELEBRATIONS",
    src: "/images/look-monochrome.png",
    alt: "An intimate private celebration portrait in monochrome",
  },
];

export default function MiddleExperience() {
  return (
    <div className={styles.middle}>
      <section
        className={styles.package}
        id="signature-experience"
        aria-labelledby="signature-experience-title"
      >
        <div className={styles.packageInner}>
          <header className={styles.packageHeader}>
            <p className={styles.eyebrowDark}>SIGNATURE EXPERIENCE</p>
            <h2 id="signature-experience-title">THE FOTOHVN EXPERIENCE</h2>
          </header>

          <div className={styles.packageStatement}>
            <p className={styles.price} aria-label="Eight thousand five hundred Philippine pesos">
              ₱8,500
            </p>
            <div className={styles.duration} aria-label="3 HOURS">
              <span className={styles.durationValue} aria-hidden="true">
                3
              </span>
              <span className={styles.durationUnit} aria-hidden="true">
                HOURS
              </span>
            </div>
          </div>

          <div className={styles.packageDetails}>
            <p className={styles.inclusionLabel}>Your experience includes</p>
            <ul className={styles.inclusions}>
              {inclusions.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>

            <div className={styles.packageAction}>
              <a className={styles.primaryButton} href="#inquire">
                BOOK FOTOHVN
              </a>
              <p>ONE BOOTH. ONE EXPERIENCE. YOUR LOOK.</p>
            </div>
          </div>
        </div>
      </section>

      <section
        className={styles.gallerySection}
        id="gallery"
        aria-labelledby="gallery-title"
      >
        <div className={styles.sectionInner}>
          <header className={styles.galleryHeader}>
            <p className={styles.eyebrow}>THE PHOTOGRAPHS</p>
            <h2 id="gallery-title">SEE IT IN ACTION</h2>
          </header>

          <div className={styles.gallery}>
            {galleryImages.map((image) => (
              <figure className={image.className} key={image.src}>
                <div className={styles.imageFrame}>
                  <Image
                    src={image.src}
                    alt={image.alt}
                    fill
                    sizes={image.sizes}
                    className={styles.image}
                  />
                </div>
                <figcaption>{image.caption}</figcaption>
              </figure>
            ))}
          </div>
        </div>
      </section>

      <section
        className={styles.eventsSection}
        id="events"
        aria-labelledby="events-title"
      >
        <div className={styles.sectionInner}>
          <header className={styles.eventsHeader}>
            <p className={styles.eyebrow}>CELEBRATIONS</p>
            <h2 id="events-title">MADE FOR MOMENTS LIKE THESE</h2>
          </header>

          <ol className={styles.eventsList}>
            {events.map((event, index) => (
              <li className={styles.event} key={event.title}>
                <div className={styles.eventMedia}>
                  <Image
                    src={event.src}
                    alt={event.alt}
                    fill
                    sizes="(max-width: 767px) 100vw, 58vw"
                    className={styles.image}
                  />
                </div>
                <div className={styles.eventTitleBlock}>
                  <span className={styles.eventNumber} aria-hidden="true">
                    {String(index + 1).padStart(2, "0")}
                  </span>
                  <h3>{event.title}</h3>
                </div>
              </li>
            ))}
          </ol>
        </div>
      </section>
    </div>
  );
}
