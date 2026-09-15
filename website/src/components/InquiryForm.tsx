"use client";

import type { FormEvent } from "react";
import styles from "./ClosingExperience.module.css";

export default function InquiryForm() {
  function prepareEmail(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const fields = new FormData(event.currentTarget);
    const body = Array.from(fields, ([key, value]) => `${key}: ${value}`).join(
      "\r\n",
    );
    window.location.href = `mailto:hello@fotohavn.ph?subject=FOTOHAVN%20inquiry&body=${encodeURIComponent(body)}`;
  }
  return (
    <form
      className={styles.form}
      action="mailto:hello@fotohavn.ph?subject=FOTOHAVN%20inquiry"
      method="post"
      encType="text/plain"
      onSubmit={prepareEmail}
      aria-describedby="inquiry-note"
    >
      <fieldset className={styles.intentField}>
        <legend>What would you like to ask about?</legend>
        <label>
          <input name="Intent" type="radio" value="Mall booth" required />
          <span>Mall booth</span>
        </label>
        <label>
          <input name="Intent" type="radio" value="Event rental" required />
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
        <textarea id="inquiry-notes" name="Notes" rows={3} />
      </div>
      <div className={styles.formFooter}>
        <p id="inquiry-note">
          Submitting opens your email app with these details ready to send.
        </p>
        <button className="button" type="submit">
          START THE CONVERSATION <span aria-hidden="true">↗</span>
        </button>
      </div>
    </form>
  );
}
