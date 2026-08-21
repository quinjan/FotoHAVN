import Image from 'next/image'

import styles from './ClosingExperience.module.css'

export function ClosingExperience() {
  return (
    <>
      <section id="story" className={styles.story} aria-labelledby="story-title">
        <div className={styles.container}>
          <div className={styles.storyGrid}>
            <div className={styles.storyCopy}>
              <p className={styles.eyebrow}>Our story</p>
              <h2 id="story-title" className={styles.storyTitle}>
                MORE THAN A PHOTOBOOTH.
              </h2>
              <p className={styles.storyBody}>
                FOTOHVN was created for people who want more than a quick snapshot. Step
                inside, draw the curtain, and take a little time to laugh, experiment, and
                make something together. Our vintage-inspired booth pairs an intimate
                experience with distinctive photographic looks and physical prints—keepsakes
                made to be held, shared, and kept long after the celebration.
              </p>
            </div>

            <figure className={styles.storyFigure}>
              <div className={styles.storyImageFrame}>
                <Image
                  className={styles.image}
                  src="/images/booth-detail.png"
                  alt="The curtain and tactile details inside the FOTOHVN booth"
                  fill
                  sizes="(max-width: 767px) calc(100vw - 48px), (max-width: 1100px) calc(100vw - 96px), 54vw"
                />
              </div>
              <figcaption className={styles.caption}>
                A little room for unguarded photographs.
              </figcaption>
            </figure>
          </div>
        </div>
      </section>

      <section className={styles.finalCallout} aria-labelledby="final-callout-title">
        <div className={styles.calloutFrame}>
          <div className={styles.calloutImageFrame}>
            <Image
              className={styles.image}
              src="/images/candid-guests.png"
              alt="Guests sharing a candid moment with FOTOHVN"
              fill
              sizes="(max-width: 767px) 100vw, 57vw"
            />
          </div>
          <div className={styles.calloutCopy}>
            <p className={styles.eyebrow}>Bring the booth</p>
            <h2 id="final-callout-title" className={styles.calloutTitle}>
              LET&apos;S MAKE SOMETHING WORTH KEEPING.
            </h2>
            <p className={styles.calloutBody}>
              Bring FOTOHVN to your next celebration.
            </p>
            <a className={styles.primaryAction} href="#inquire">
              INQUIRE NOW
            </a>
          </div>
        </div>
      </section>

      <section id="inquire" className={styles.inquiry} aria-labelledby="inquiry-title">
        <div className={styles.container}>
          <div className={styles.inquiryGrid}>
            <div className={styles.inquiryIntro}>
              <p className={styles.eyebrow}>Inquire</p>
              <h2 id="inquiry-title" className={styles.inquiryTitle}>
                Tell us about your celebration.
              </h2>
              <p className={styles.inquiryBody}>
                Share a few details and we&apos;ll continue the conversation by email.
              </p>
              <a className={styles.emailLink} href="mailto:hello@fotohavn.ph">
                hello@fotohavn.ph
              </a>
            </div>

            <form
              className={styles.inquiryForm}
              action="mailto:hello@fotohavn.ph?subject=FOTOHVN%20inquiry"
              method="post"
              encType="text/plain"
              aria-describedby="inquiry-note"
            >
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
                <label htmlFor="inquiry-date">Event date</label>
                <input id="inquiry-date" name="Event date" type="date" required />
              </div>
              <div className={styles.field}>
                <label htmlFor="inquiry-event">Event</label>
                <select id="inquiry-event" name="Event" defaultValue="" required>
                  <option value="" disabled>
                    Select an event
                  </option>
                  <option>Wedding</option>
                  <option>Debut</option>
                  <option>Birthday</option>
                  <option>Corporate event</option>
                  <option>Private celebration</option>
                </select>
              </div>
              <div className={`${styles.field} ${styles.fullField}`}>
                <label htmlFor="inquiry-location">Location</label>
                <input
                  id="inquiry-location"
                  name="Location"
                  type="text"
                  autoComplete="street-address"
                  required
                />
              </div>
              <div className={`${styles.field} ${styles.fullField}`}>
                <label htmlFor="inquiry-notes">A little more about the day</label>
                <textarea id="inquiry-notes" name="Details" rows={4} />
              </div>
              <div className={styles.formFooter}>
                <p id="inquiry-note" className={styles.formNote}>
                  Submitting opens your email app with these details ready to send.
                </p>
                <button className={styles.submitButton} type="submit">
                  Inquire now
                </button>
              </div>
            </form>
          </div>
        </div>
      </section>

      <footer className={styles.footer}>
        <div className={styles.footerInner}>
          <div>
            <p className={styles.footerBrand}>FOTOHVN</p>
            <p className={styles.footerLine}>PHOTOGRAPHS, DEVELOPED DIFFERENTLY.</p>
          </div>
          <nav className={styles.footerNavigation} aria-label="Social and contact links">
            <a href="https://www.instagram.com/fotohavn" target="_blank" rel="noreferrer">
              Instagram
            </a>
            <a href="https://www.facebook.com/fotohavn" target="_blank" rel="noreferrer">
              Facebook
            </a>
            <a href="mailto:hello@fotohavn.ph">Email</a>
          </nav>
          <p className={styles.copyright}>© 2026 FOTOHVN</p>
        </div>
      </footer>
    </>
  )
}

export default ClosingExperience
