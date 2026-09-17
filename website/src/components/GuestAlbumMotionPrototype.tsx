"use client";

// THROWAWAY: compare continuous motion (A) with native swipe + smooth buttons (B),
// on the existing homepage via ?variant=A|B. Await a choice before production work.
import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import GuestBoardPhoto from "./GuestBoardPhoto";
import { guestPhotographs } from "./guestBoardData";
import { withSiteBasePath } from "../../site.config";
import styles from "./GuestAlbumMotionPrototype.module.css";

const count = guestPhotographs.length;
const photos = [...guestPhotographs, ...guestPhotographs, ...guestPhotographs];
const modulo = (n: number) => ((n % count) + count) % count;

export default function GuestAlbumMotionPrototype() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const variant = params.get("variant");
  const enabled = variant === "A" || variant === "B";
  function change() {
    const next = new URLSearchParams(params.toString());
    next.set("variant", variant === "A" ? "B" : "A");
    router.replace(`${pathname}?${next}#guest-album`, { scroll: false });
  }
  useEffect(() => {
    if (!enabled) return;
    const key = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement;
      if (target.closest("input, textarea, select, [contenteditable], [data-album-prototype]")) return;
      if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
        event.preventDefault();
        const next = new URLSearchParams(window.location.search);
        next.set("variant", variant === "A" ? "B" : "A");
        router.replace(`${pathname}?${next}#guest-album`, { scroll: false });
      }
    };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, [enabled, variant, pathname, router]);
  if (!enabled) return null;
  return <>
    <AlbumMotion key={variant} variant={variant} />
    <nav className={styles.switcher} aria-label="Animation prototype variants">
      <button onClick={change} aria-label="Previous variant">←</button>
      <span><small>MOBILE PROTOTYPE · AWAITING YOUR PICK</small>{variant === "A" ? "A — Continuous ribbon" : "B — Smooth swipe"}</span>
      <button onClick={change} aria-label="Next variant">→</button>
    </nav>
  </>;
}

function AlbumMotion({ variant }: { variant: "A" | "B" }) {
  const scroller = useRef<HTMLDivElement>(null);
  const step = useRef(1);
  const position = useRef(count);
  const visible = useRef(false);
  const touching = useRef(false);
  const [index, setIndex] = useState(0);
  const [note, setNote] = useState(false);
  const [paused, setPaused] = useState(false);
  const [direction, setDirection] = useState(1);
  const [reduce, setReduce] = useState(false);
  const photo = guestPhotographs[index];

  useLayoutEffect(() => {
    const element = scroller.current!;
    const measure = () => {
      const cards = element.children;
      step.current = cards[1].getBoundingClientRect().left - cards[0].getBoundingClientRect().left;
      element.scrollTo({ left: position.current * step.current, behavior: "instant" });
    };
    const resize = new ResizeObserver(measure);
    resize.observe(element);
    const observer = new IntersectionObserver(([entry]) => { visible.current = entry.isIntersecting; });
    observer.observe(element);
    measure();
    return () => { resize.disconnect(); observer.disconnect(); };
  }, []);

  useEffect(() => {
    const preference = matchMedia("(prefers-reduced-motion: reduce)");
    const update = () => setReduce(preference.matches);
    update();
    preference.addEventListener("change", update);
    return () => preference.removeEventListener("change", update);
  }, []);

  useEffect(() => {
    if (variant !== "A" || paused || note || reduce) return;
    const element = scroller.current!;
    let frame = 0;
    let last = 0;
    let offset = element.scrollLeft;
    const tick = (now: number) => {
      const dt = Math.min(now - (last || now), 40);
      last = now;
      if (visible.current && !document.hidden && !touching.current) {
        offset += direction * dt * 0.032;
        const cycle = count * step.current;
        if (offset < cycle / 2) offset += cycle;
        if (offset > cycle * 2) offset -= cycle;
        element.scrollLeft = offset;
      } else offset = element.scrollLeft;
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [variant, paused, note, direction, reduce]);

  function settle() {
    const element = scroller.current!;
    let slot = Math.round(element.scrollLeft / step.current);
    if (slot < count || slot >= count * 2) {
      slot = count + modulo(slot);
      element.scrollTo({ left: slot * step.current, behavior: "instant" });
    }
    position.current = slot;
    setIndex(modulo(slot));
  }
  function move(delta: number) {
    setNote(false);
    if (variant === "A") setPaused(true);
    const element = scroller.current!;
    position.current = Math.round(element.scrollLeft / step.current) + delta;
    element.scrollTo({ left: position.current * step.current, behavior: reduce ? "instant" : "smooth" });
  }
  return <div className={styles.prototype} data-album-prototype={variant}>
    <div className={styles.stage} style={{ "--material": `url("${withSiteBasePath("/images/guest-board/metal-board.webp")}")` } as CSSProperties}>
      <div ref={scroller} className={styles.track} data-mode={variant} tabIndex={0}
        role="region" aria-label="Guest photographs" onKeyDown={event => {
          if (event.key === "ArrowLeft" || event.key === "ArrowRight") { event.preventDefault(); move(event.key === "ArrowLeft" ? -1 : 1); }
        }} onPointerDown={() => { touching.current = true; if (variant === "A") setPaused(true); }}
        onPointerUp={() => { touching.current = false; }} onPointerCancel={() => { touching.current = false; }}
        onScroll={() => {
          const next = modulo(Math.round(scroller.current!.scrollLeft / step.current));
          setIndex(next);
        }} onScrollEnd={() => { if (variant === "B" || paused) settle(); }}>
        {photos.map((item, slot) => <div key={`${slot}-${item.id}`} className={styles.slot} aria-hidden={modulo(slot) !== index || slot < count || slot >= count * 2}>
          <div className={styles.print} data-kind={item.kind} style={{ "--tilt": `${slot % 2 ? -2 : 2}deg` } as CSSProperties}>
            <div className={styles.image}><GuestBoardPhoto photo={item} sizes="(max-width: 767px) 76vw, 420px" priority={slot >= count - 1 && slot <= count + 2} /></div>
            <p>{item.title}</p>
          </div>
        </div>)}
      </div>
    </div>
    {variant === "A" && <div className={styles.ribbonControls}>
      <button onClick={() => setPaused(!paused)} disabled={reduce}>{reduce ? "Motion reduced" : paused ? "Play ribbon" : "Pause ribbon"}</button>
      <button onClick={() => setDirection(-direction)} aria-label="Reverse ribbon direction">{direction === 1 ? "Moving ←" : "Moving →"}</button>
    </div>}
    <div className={styles.controls}>
      <button onClick={() => move(-1)} aria-label="Previous photograph">←</button>
      <button onClick={() => { setNote(!note); if (variant === "A") setPaused(true); }} aria-expanded={note}> {note ? "Close the note" : "Read the note"}</button>
      <button onClick={() => move(1)} aria-label="Next photograph">→</button>
    </div>
    {note && <div className={styles.note}>
      <small>{photo.testimonial ? "IN THEIR WORDS" : "FROM THE FOTOHAVN ALBUM"}</small><h3>{photo.title}</h3>
      <p>{photo.testimonial?.quote ?? photo.note}</p>
      <small>{photo.testimonial?.attribution ?? "An editorial note from FOTOHAVN."}</small>
    </div>}
    <p className={styles.state} aria-live={variant === "B" ? "polite" : "off"}>{index + 1} / {count} · {variant === "A" ? reduce ? "Still · reduced motion" : paused || note ? "Paused" : "32 px / second" : "Swipe to explore · smooth button glide"}</p>
  </div>;
}
