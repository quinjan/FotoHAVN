import ExperienceMotion from "./ExperienceMotion";
import { inquirySectionId, instagramMessageUrl } from "../../site.config";
import styles from "./ClosingExperience.module.css";

export default function ClosingExperience() {
  return (
    <>
      <ExperienceMotion>
        <section
          id={inquirySectionId}
          className={styles.begin}
          aria-labelledby="begin-heading"
        >
          <div className={styles.beginHeading} data-reveal>
            <h2 id="begin-heading">
              Let’s make
              <br />
              something
              <br />
              <em>worth keeping.</em>
            </h2>
            <p>
              A spontaneous stop. A celebration you’ve been planning. There’s a
              little room for both.
            </p>
          </div>
          <div className={styles.paths}>
            <article id="find-the-booth" data-reveal>
              <p className={styles.pathLabel}>MAKE A LITTLE DETOUR</p>
              <h3>Find the booth.</h3>
              <p>
                Drop by for a pay-per-use visit. Ask us for the current
                location, hours, and details before you go.
              </p>
              <a
                className="textLink"
                href={instagramMessageUrl}
                target="_blank"
                rel="noreferrer"
              >
                ASK FOR CURRENT DETAILS <span aria-hidden="true">↗</span>
              </a>
            </article>
            <article id="rent-fotohavn" data-reveal>
              <p className={styles.pathLabel}>BRING EVERYONE TOGETHER</p>
              <h3>Rent FOTOHAVN.</h3>
              <p>
                Your celebration, our little haven. Event rental is offered by
                the hour with unlimited prints.
              </p>
              <a
                className="textLink"
                href={instagramMessageUrl}
                target="_blank"
                rel="noreferrer"
              >
                ASK ABOUT YOUR DATE <span aria-hidden="true">↗</span>
              </a>
            </article>
          </div>
        </section>
      </ExperienceMotion>
      <footer className={styles.footer}>
        <div className={styles.footerTop}>
          <p>
            PHOTOGRAPHS,
            <br />
            DEVELOPED DIFFERENTLY.
          </p>
          <nav aria-label="Social links">
            <a
              href="https://www.instagram.com/fotohavn.ph/"
              target="_blank"
              rel="noreferrer"
            >
              Instagram <span aria-hidden="true">↗</span>
            </a>
            <a
              href="https://www.facebook.com/profile.php?id=61593369275724"
              target="_blank"
              rel="noreferrer"
            >
              Facebook <span aria-hidden="true">↗</span>
            </a>
            <a
              href="https://www.tiktok.com/@fotohavn.ph"
              target="_blank"
              rel="noreferrer"
            >
              TikTok <span aria-hidden="true">↗</span>
            </a>
          </nav>
        </div>
        <p className={styles.footerBrand}>FOTOHAVN</p>
        <div className={styles.footerBottom}>
          <p>© 2026 FOTOHAVN</p>
          <a href="#top">
            BACK TO TOP <span aria-hidden="true">↑</span>
          </a>
        </div>
      </footer>
    </>
  );
}
