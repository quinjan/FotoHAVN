import { ArrowRightIcon } from "@phosphor-icons/react/dist/ssr/ArrowRight";
import Image from "./EventPhoto";
import { withSiteBasePath } from "../../site.config";
import ExperienceMotion from "./ExperienceMotion";
import GuestAlbum from "./GuestAlbum";
import styles from "./MiddleExperience.module.css";

export default function MiddleExperience() {
  return (
    <>
      <ExperienceMotion>
        <section
          id="prints"
          className={styles.prints}
          aria-labelledby="prints-heading"
        >
          <div className={styles.printCopy} data-reveal>
            <h2 id="prints-heading">
              Keep the
              <br />
              moment <em>close.</em>
            </h2>
            <p>
              A few minutes inside. A memory that stays with you.
            </p>
            <p className={styles.smallCopy}>
              Leave with physical prints. Scan the QR code for digital copies,
              ready to share and keep.
            </p>
            <div className={styles.printLinks}>
              <a href="#guest-album" className="textLink">
                SEE THE GOOD COMPANY <span aria-hidden="true">↘</span>
              </a>
              <a
                href={withSiteBasePath("/online")}
                className={`textLink ${styles.digitalLink}`}
              >
                MAKE A DIGITAL STRIP
                <ArrowRightIcon size={20} weight="light" aria-hidden="true" />
              </a>
            </div>
          </div>
          <div className={styles.printComposition}>
            <figure className={styles.printPhoto} data-print>
              <Image
                src={withSiteBasePath("/images/evia/keep-the-moment-close.webp")}
                alt="Two friends exchange color and black-and-white FOTOHAVN Photo Strips beside the booth."
                fill
                sizes="(max-width: 767px) 82vw, 42vw"
              />
            </figure>
            <p className={styles.printCaption}>
              MADE TO BE HELD, SHARED, AND KEPT.
            </p>
          </div>
        </section>
      </ExperienceMotion>
      <GuestAlbum />
    </>
  );
}
