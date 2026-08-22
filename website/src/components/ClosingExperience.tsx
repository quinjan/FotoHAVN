import styles from "./ClosingExperience.module.css";

export default function ClosingExperience() {
  return (
    <>
      <section
        className={styles.actionSection}
        aria-labelledby="action-heading"
      >
        <div className={styles.container}>
          <header className={styles.actionHeader}>
            <p className={styles.sectionLabel}>BEGIN HERE</p>
            <h2 id="action-heading">CHOOSE HOW YOU WANT TO BEGIN.</h2>
          </header>

          <div className={styles.pathSplit}>
            <article id="find-a-booth" className={styles.mallPath}>
              <p className={styles.pathLabel}>MALL BOOTH</p>
              <h3>FIND A BOOTH</h3>
              <p>
                Current mall location, hours, price, payment methods, and session
                details must be confirmed.
              </p>
              <a href="mailto:hello@fotohavn.ph?subject=Current%20FOTOHVN%20booth">
                ASK FOR CURRENT DETAILS
              </a>
            </article>

            <article id="rent-fotohavn" className={styles.eventPath}>
              <p className={styles.pathLabel}>EVENT RENTAL</p>
              <h3>RENT FOTOHVN</h3>
              <p>Event rental is offered by the hour with unlimited prints.</p>
              <p>Share your event date and city or venue to begin.</p>
              <a href="#inquiry">ASK ABOUT YOUR DATE</a>
            </article>
          </div>

          <div id="inquiry" className={styles.inquiryGrid}>
            <div className={styles.inquiryIntro}>
              <p className={styles.sectionLabel}>INQUIRY</p>
              <h3>START WITH WHAT YOU KNOW.</h3>
              <p>
                Choose the path that fits, then share only the details you have.
              </p>
              <a href="mailto:hello@fotohavn.ph">hello@fotohavn.ph</a>
            </div>

            <form
              className={styles.inquiryForm}
              action="mailto:hello@fotohavn.ph?subject=FOTOHVN%20inquiry"
              method="post"
              encType="text/plain"
              aria-describedby="inquiry-note"
            >
              <fieldset className={styles.intentField}>
                <legend>What would you like to ask about?</legend>
                <label>
                  <input
                    name="Intent"
                    type="radio"
                    value="Mall booth"
                    required
                  />
                  <span>Mall booth</span>
                </label>
                <label>
                  <input
                    name="Intent"
                    type="radio"
                    value="Event rental"
                    required
                  />
                  <span>Event rental</span>
                </label>
              </fieldset>

              <div className={styles.field}>
                <label htmlFor="inquiry-name">Name</label>
                <input
                  id="inquiry-name"
                  name="Name"
                  type="text"
                  autoComplete="name"
                  required
                />
              </div>

              <div className={styles.field}>
                <label htmlFor="inquiry-email">Email</label>
                <input
                  id="inquiry-email"
                  name="Email"
                  type="email"
                  autoComplete="email"
                  required
                />
              </div>

              <div className={styles.field}>
                <label htmlFor="inquiry-date">Event date (optional)</label>
                <input id="inquiry-date" name="Event date" type="date" />
              </div>

              <div className={styles.field}>
                <label htmlFor="inquiry-place">City or venue (optional)</label>
                <input
                  id="inquiry-place"
                  name="City or venue"
                  type="text"
                  autoComplete="address-level2"
                />
              </div>

              <div className={styles.fullField}>
                <label htmlFor="inquiry-notes">Notes (optional)</label>
                <textarea id="inquiry-notes" name="Notes" rows={5} />
              </div>

              <div className={styles.formFooter}>
                <p id="inquiry-note">
                  Submitting opens your email app with these details ready to
                  send.
                </p>
                <button type="submit">START THE CONVERSATION</button>
              </div>
            </form>
          </div>
        </div>
      </section>

      <footer className={styles.footer}>
        <div className={styles.footerInner}>
          <div>
            <p className={styles.footerBrand}>FOTOHVN</p>
            <p className={styles.footerLine}>
              PHOTOGRAPHS, DEVELOPED DIFFERENTLY.
            </p>
          </div>
          <nav
            className={styles.footerNavigation}
            aria-label="Social and contact links"
          >
            <a
              href="https://www.instagram.com/fotohavn"
              target="_blank"
              rel="noreferrer"
            >
              Instagram
            </a>
            <a
              href="https://www.facebook.com/fotohavn"
              target="_blank"
              rel="noreferrer"
            >
              Facebook
            </a>
            <a href="mailto:hello@fotohavn.ph">Email</a>
          </nav>
          <p className={styles.copyright}>© 2026 FOTOHVN</p>
        </div>
      </footer>
    </>
  );
}
