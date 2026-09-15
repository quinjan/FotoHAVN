"use client";

import { useEffect, useRef, type ReactNode } from "react";

export default function ExperienceMotion({
  children,
}: {
  children: ReactNode;
}) {
  const root = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const element = root.current;
    if (!element) return;
    let cancelled = false;
    let cleanup: (() => void) | undefined;
    const observer = new IntersectionObserver(([entry]) => {
      if (!entry.isIntersecting) return;
      observer.disconnect();
      void initialize();
    });
    observer.observe(element);
    async function initialize() {
      try {
        const [{ default: gsap }, { ScrollTrigger }] = await Promise.all([
          import("gsap"),
          import("gsap/ScrollTrigger"),
        ]);
        if (cancelled) return;
        gsap.registerPlugin(ScrollTrigger);
        const media = gsap.matchMedia();
        const context = gsap.context(() => {
          media.add("(prefers-reduced-motion: no-preference)", () => {
            gsap.utils
              .toArray<HTMLElement>("[data-reveal]", root.current)
              .forEach((element) => {
                // Do not hide content the visitor can already see while the chunk loads.
                if (
                  element.getBoundingClientRect().top <
                  window.innerHeight * 0.94
                )
                  return;
                gsap.from(element, {
                  y: 30,
                  opacity: 0,
                  duration: 0.9,
                  ease: "power3.out",
                  scrollTrigger: {
                    trigger: element,
                    start: "top 94%",
                    once: true,
                  },
                });
              });
            gsap.utils
              .toArray<HTMLElement>("[data-float]", root.current)
              .forEach((element) => {
                const direction = Number(element.dataset.float);
                gsap.fromTo(
                  element,
                  { y: 50, rotation: direction * 7 },
                  {
                    y: -30,
                    rotation: direction * 2,
                    ease: "none",
                    scrollTrigger: {
                      trigger: element.parentElement,
                      start: "top bottom",
                      end: "bottom top",
                      scrub: 1.2,
                    },
                  },
                );
              });
            gsap.utils
              .toArray<HTMLElement>("[data-print]", root.current)
              .forEach((element) => {
                gsap.from(element, {
                  yPercent: 14,
                  rotation: -5,
                  ease: "none",
                  scrollTrigger: {
                    trigger: element,
                    start: "top 90%",
                    end: "center 55%",
                    scrub: 1,
                  },
                });
              });
          });
        }, root);
        cleanup = () => {
          media.revert();
          context.revert();
        };
      } catch {
        // Motion is progressive enhancement; the server-rendered content remains usable.
      }
    }
    return () => {
      cancelled = true;
      observer.disconnect();
      cleanup?.();
    };
  }, []);
  return <div ref={root}>{children}</div>;
}
