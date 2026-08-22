"use client";

import { useEffect, useRef, useState } from "react";

import styles from "./SiteChrome.module.css";

const navigation = [
  { href: "#experience", label: "EXPERIENCE" },
  { href: "#the-booth", label: "THE BOOTH" },
  { href: "#prints", label: "PRINTS" },
] as const;

export default function SiteChrome() {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const headerRef = useRef<HTMLElement>(null);
  const menuButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const updateNavigation = () => setIsScrolled(window.scrollY > 24);

    updateNavigation();
    window.addEventListener("scroll", updateNavigation, { passive: true });
    return () => window.removeEventListener("scroll", updateNavigation);
  }, []);

  useEffect(() => {
    if (!isMenuOpen) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;

      event.preventDefault();
      setIsMenuOpen(false);
      requestAnimationFrame(() => menuButtonRef.current?.focus());
    };

    const handlePointerDown = (event: PointerEvent) => {
      if (headerRef.current?.contains(event.target as Node)) return;
      setIsMenuOpen(false);
    };

    document.addEventListener("keydown", handleKeyDown);
    document.addEventListener("pointerdown", handlePointerDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.removeEventListener("pointerdown", handlePointerDown);
    };
  }, [isMenuOpen]);

  const closeMenu = () => setIsMenuOpen(false);
  const headerClassName = [
    styles.header,
    isScrolled ? styles.scrolled : "",
    isMenuOpen ? styles.menuOpen : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <header ref={headerRef} className={headerClassName}>
      <div className={styles.bar}>
        <a
          className={styles.brand}
          href="#top"
          aria-label="FOTOHVN, back to the top"
          onClick={closeMenu}
        >
          FOTOHVN
        </a>

        <nav className={styles.desktopNavigation} aria-label="Primary navigation">
          <ul className={styles.navigationList}>
            {navigation.map((item) => (
              <li key={item.href}>
                <a className={styles.navigationLink} href={item.href}>
                  {item.label}
                </a>
              </li>
            ))}
          </ul>
        </nav>

        <div className={styles.desktopActions}>
          <a className={styles.findAction} href="#find-a-booth">
            FIND A BOOTH
          </a>
          <a className={styles.rentAction} href="#rent-fotohavn">
            RENT FOTOHVN
          </a>
        </div>

        <button
          ref={menuButtonRef}
          className={styles.menuButton}
          type="button"
          aria-expanded={isMenuOpen}
          aria-controls="mobile-navigation"
          onClick={() => setIsMenuOpen((isOpen) => !isOpen)}
        >
          {isMenuOpen ? "CLOSE" : "MENU"}
        </button>
      </div>

      <nav
        id="mobile-navigation"
        className={styles.mobileNavigation}
        aria-label="Mobile navigation"
        hidden={!isMenuOpen}
      >
        <ul className={styles.mobileNavigationList}>
          {navigation.map((item) => (
            <li key={item.href}>
              <a
                className={styles.mobileNavigationLink}
                href={item.href}
                onClick={closeMenu}
              >
                {item.label}
              </a>
            </li>
          ))}
        </ul>
        <div className={styles.mobileActions}>
          <a
            className={styles.mobileFindAction}
            href="#find-a-booth"
            onClick={closeMenu}
          >
            FIND A BOOTH
          </a>
          <a
            className={styles.mobileRentAction}
            href="#rent-fotohavn"
            onClick={closeMenu}
          >
            RENT FOTOHVN
          </a>
        </div>
      </nav>
    </header>
  );
}
