"use client";

import { useEffect, useRef, useState } from "react";
import {
  findBoothSectionHash,
  rentFotohavnSectionHash,
  withSiteBasePath,
} from "../../site.config";

import styles from "./SiteChrome.module.css";

const navigation = [
  { href: "#experience", label: "EXPERIENCE" },
  { href: "#the-booth", label: "THE BOOTH" },
  { href: "#prints", label: "PRINTS" },
  { href: withSiteBasePath("/online"), label: "ONLINE BOOTH" },
] as const;

export default function SiteChrome() {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const headerRef = useRef<HTMLElement>(null);
  const menuButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const sentinel = document.getElementById("top-sentinel");
    if (!sentinel) return;
    const observer = new IntersectionObserver(([entry]) =>
      setIsScrolled(!entry.isIntersecting),
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const desktop = window.matchMedia("(min-width: 1024px)");
    const closeOnDesktop = () => {
      if (desktop.matches) setIsMenuOpen(false);
    };
    desktop.addEventListener("change", closeOnDesktop);
    return () => desktop.removeEventListener("change", closeOnDesktop);
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
          aria-label="FOTOHAVN, back to the top"
          onClick={closeMenu}
        >
          FOTOHAVN
        </a>

        <nav
          className={styles.desktopNavigation}
          aria-label="Primary navigation"
        >
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
          <a
            className={styles.findAction}
            href={findBoothSectionHash}
          >
            FIND THE BOOTH
          </a>
          <a
            className={styles.rentAction}
            href={rentFotohavnSectionHash}
          >
            RENT FOTOHAVN
          </a>
        </div>

        <button
          ref={menuButtonRef}
          className={styles.menuButton}
          type="button"
          aria-label={isMenuOpen ? "Close menu" : "Open menu"}
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
            href={findBoothSectionHash}
            onClick={closeMenu}
          >
            FIND THE BOOTH
          </a>
          <a
            className={styles.mobileRentAction}
            href={rentFotohavnSectionHash}
            onClick={closeMenu}
          >
            RENT FOTOHAVN
          </a>
        </div>
      </nav>
    </header>
  );
}
