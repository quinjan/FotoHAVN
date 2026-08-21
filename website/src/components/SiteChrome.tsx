'use client'

import { useEffect, useRef, useState } from 'react'

import styles from './SiteChrome.module.css'

const navigation = [
  { href: '#experience', label: 'Experience' },
  { href: '#photographic-looks', label: 'Looks' },
  { href: '#signature-experience', label: 'Package' },
  { href: '#gallery', label: 'Gallery' },
  { href: '#events', label: 'Events' },
  { href: '#story', label: 'Story' },
]

export function SiteChrome() {
  const [isScrolled, setIsScrolled] = useState(false)
  const [isMenuOpen, setIsMenuOpen] = useState(false)
  const headerRef = useRef<HTMLElement>(null)
  const menuButtonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    const handleScroll = () => setIsScrolled(window.scrollY > 24)

    window.addEventListener('scroll', handleScroll, { passive: true })
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  useEffect(() => {
    if (!isMenuOpen) return

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return

      event.preventDefault()
      setIsMenuOpen(false)
      requestAnimationFrame(() => menuButtonRef.current?.focus())
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (headerRef.current?.contains(event.target as Node)) return
      setIsMenuOpen(false)
    }

    document.addEventListener('keydown', handleKeyDown)
    document.addEventListener('pointerdown', handlePointerDown)

    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      document.removeEventListener('pointerdown', handlePointerDown)
    }
  }, [isMenuOpen])

  const closeMenu = () => setIsMenuOpen(false)

  const headerClassName = [
    styles.header,
    isScrolled ? styles.scrolled : '',
    isMenuOpen ? styles.menuOpen : '',
  ]
    .filter(Boolean)
    .join(' ')

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

        <a className={styles.bookingLink} href="#inquire">
          BOOK FOTOHVN
        </a>

        <button
          ref={menuButtonRef}
          className={styles.menuButton}
          type="button"
          aria-expanded={isMenuOpen}
          aria-controls="mobile-navigation"
          onClick={() => setIsMenuOpen((isOpen) => !isOpen)}
        >
          {isMenuOpen ? 'CLOSE' : 'MENU'}
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
        <a className={styles.mobileBookingLink} href="#inquire" onClick={closeMenu}>
          BOOK FOTOHVN
        </a>
      </nav>
    </header>
  )
}

export default SiteChrome
