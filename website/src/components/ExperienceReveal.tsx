"use client";

import { useEffect, useRef, type ReactNode } from "react";

/** Small, one-time reveals that leave the lift and its intrinsic sizing alone. */
export default function ExperienceReveal({ children }: { children: ReactNode }) {
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const element = root.current;
    if (!element) return;

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    if (reducedMotion.matches) return;

    const animations = new Set<Animation>();
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        observer.unobserve(entry.target);
        if (reducedMotion.matches) continue;

        const animation = entry.target.animate(
          [
            { opacity: 0, transform: "translateY(16px)" },
            { opacity: 1, transform: "translateY(0)" },
          ],
          { duration: 600, easing: "cubic-bezier(0.22, 1, 0.36, 1)" },
        );
        animations.add(animation);
        animation.onfinish = () => animations.delete(animation);
      }
    }, { rootMargin: "0px 0px -5% 0px", threshold: 0.1 });

    element.querySelectorAll<HTMLElement>("[data-experience-reveal]").forEach((target) => {
      // A late hydration must never hide a sentence that is already being read.
      if (target.getBoundingClientRect().top < window.innerHeight * 0.95) return;
      observer.observe(target);
    });

    const settle = () => {
      observer.disconnect();
      animations.forEach((animation) => animation.cancel());
      animations.clear();
    };
    reducedMotion.addEventListener("change", settle);

    return () => {
      settle();
      reducedMotion.removeEventListener("change", settle);
    };
  }, []);

  return <div ref={root}>{children}</div>;
}