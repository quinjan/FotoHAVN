"use client";

import { useEffect, useLayoutEffect, useRef, type CSSProperties, type Ref } from "react";
import Image from "next/image";
import { ArrowLeftIcon } from "@phosphor-icons/react/dist/csr/ArrowLeft";
import { ArrowRightIcon } from "@phosphor-icons/react/dist/csr/ArrowRight";
import { DeviceRotateIcon } from "@phosphor-icons/react/dist/csr/DeviceRotate";
import GuestBoardPhoto from "./GuestBoardPhoto";
import { guestPhotographs } from "./guestBoardData";
import { withSiteBasePath } from "../../site.config";
import album from "./GuestAlbum.module.css";
import styles from "./GuestAlbumCarousel.module.css";

const count = guestPhotographs.length;
// Keep one full copy on each side so a wrap has the same neighbours and geometry.
const photographs = [...guestPhotographs, ...guestPhotographs, ...guestPhotographs];
const indexOf = (slot: number) => ((slot % count) + count) % count;
const asset = (name: string) => withSiteBasePath(`/images/guest-board/${name}`);
// Seed each tilt by photograph identity: irregular, but stable across renders and loop copies.
function printTilt(id: string) {
  const seed = [...id].reduce((hash, character) => Math.imul(hash, 31) + character.charCodeAt(0) | 0, 7);
  return ((seed >>> 0) % 19 - 9) / 2;
}
const keepsakes: Record<string, { image: string; width: number; height: number; placement: string }> = {
  "guest-trio": { image: "teddy-charm.webp", width: 480, height: 720, placement: "right" },
  "friends": { image: "ribbon-keepsake.webp", width: 480, height: 525, placement: "left" },
  "family": { image: "bunny-charm.webp", width: 480, height: 720, placement: "left" },
  "keepsakes": { image: "ticket-keepsake.webp", width: 480, height: 285, placement: "bottom" },
  "guest-flowers": { image: "ribbon-keepsake.webp", width: 480, height: 525, placement: "right" },
  "guest-together": { image: "ticket-keepsake.webp", width: 480, height: 285, placement: "bottom" },
};

export default function GuestAlbumCarousel({ selectedIndex, side, onSelect, onFlip, flipRef }: {
  selectedIndex: number;
  side: "photo" | "note";
  onSelect: (index: number) => void;
  onFlip: () => void;
  flipRef: Ref<HTMLButtonElement>;
}) {
  const track = useRef<HTMLDivElement>(null);
  const step = useRef(0);
  const target = useRef(count + selectedIndex);
  const pending = useRef(false);
  const touching = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const latest = useRef({ selectedIndex, onSelect });
  useLayoutEffect(() => { latest.current = { selectedIndex, onSelect }; });

  useLayoutEffect(() => {
    const element = track.current!;
    const measure = () => {
      // display:none during landscape/desktop must not destroy the saved position.
      if (!element.clientWidth) return;
      const cards = element.children;
      step.current = cards[1].getBoundingClientRect().left - cards[0].getBoundingClientRect().left;
      target.current = count + latest.current.selectedIndex;
      pending.current = false;
      element.scrollTo({ left: target.current * step.current, behavior: "instant" });
    };
    const resize = new ResizeObserver(measure);
    resize.observe(element);
    measure();
    return () => { resize.disconnect(); if (timer.current) clearTimeout(timer.current); };
  }, []);

  useEffect(() => {
    // A photograph selected in the landscape board also becomes the mobile selection.
    if (indexOf(target.current) !== selectedIndex) {
      target.current = count + selectedIndex;
      track.current?.scrollTo({ left: target.current * step.current, behavior: "instant" });
    }
  }, [selectedIndex]);

  function settle() {
    const element = track.current;
    if (!element?.clientWidth || !step.current || touching.current) return;
    if (timer.current) clearTimeout(timer.current);
    const slot = Math.round(element.scrollLeft / step.current);
    // Ignore an interrupted scroll's end event while its replacement is in flight.
    if (pending.current && Math.abs(element.scrollLeft - target.current * step.current) > 2) return;
    const index = indexOf(slot);
    target.current = count + index;
    pending.current = false;
    if (slot !== target.current) element.scrollTo({ left: target.current * step.current, behavior: "instant" });
    if (index !== latest.current.selectedIndex) latest.current.onSelect(index);
  }

  function scheduleSettle() {
    if (timer.current) clearTimeout(timer.current);
    // Fallback for browsers without scrollend; reset throughout native momentum.
    timer.current = setTimeout(settle, 180);
  }

  function move(direction: number) {
    const element = track.current;
    if (!element || !step.current) return;
    const current = pending.current ? target.current : Math.round(element.scrollLeft / step.current);
    target.current = Math.max(0, Math.min(photographs.length - 1, current + direction));
    pending.current = true;
    const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    element.scrollTo({ left: target.current * step.current, behavior: reduce ? "instant" : "smooth" });
    scheduleSettle();
  }

  const selected = guestPhotographs[selectedIndex];
  return <div className={styles.carousel} data-guest-carousel>
    <div className={styles.stage} style={{ "--material": `url("${withSiteBasePath("/images/guest-board/metal-board.webp")}")` } as CSSProperties}>
      <div ref={track} className={styles.track} tabIndex={0} role="region" aria-label="Explore the guest photographs"
        onKeyDown={event => {
          if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
          event.preventDefault(); move(event.key === "ArrowLeft" ? -1 : 1);
        }}
        onPointerDown={() => { touching.current = true; pending.current = false; }}
        onPointerUp={() => { touching.current = false; scheduleSettle(); }}
        onPointerCancel={() => { touching.current = false; scheduleSettle(); }}
        onScroll={scheduleSettle} onScrollEnd={settle}>
        {photographs.map((photo, slot) => {
          const active = slot === count + selectedIndex;
          const showNote = active && side === "note";
          const keepsake = keepsakes[photo.id];
          return <div key={slot} className={styles.slot} aria-hidden={!active} inert={!active}>
            <div className={`${album.portraitPrint} ${styles.print}`} data-kind={photo.kind} data-side={showNote ? "note" : "photo"}
              data-portrait-photo={active ? photo.id : undefined} style={{ "--tilt": `${printTilt(photo.id)}deg` } as CSSProperties}>
              <Image className={styles.clip} src={asset("silver-clip-small.webp")} alt="" aria-hidden="true"
                width={34} height={46} unoptimized draggable={false} />
              <div className={album.turningSheet} data-side={showNote ? "note" : "photo"}>
                <div className={`${album.portraitFront} ${styles.front}`} aria-hidden={showNote} inert={showNote}>
                  <div className={album.portraitImage}><GuestBoardPhoto photo={photo} sizes="(max-width: 767px) 76vw, 420px" /></div>
                  <p className={styles.caption}>{photo.title}</p>
                </div>
                {active && <div className={`${album.portraitBack} ${styles.back}`} aria-hidden={!showNote} inert={!showNote}
                  tabIndex={showNote ? 0 : -1} role="group" aria-label={`Note: ${photo.title}`}>
                  <p className={album.noteLabel}>{photo.testimonial ? "IN THEIR WORDS" : "FROM THE FOTOHAVN ALBUM"}</p>
                  <h3>{photo.title}</h3>
                  {photo.testimonial
                    ? <><blockquote>{photo.testimonial.quote}</blockquote><p>{photo.testimonial.attribution}</p></>
                    : <><p className={album.portraitNote}>{photo.note}</p><span className={album.captionCredit}>An editorial note from FOTOHAVN.</span></>}
                </div>}
              </div>
              {keepsake && <Image className={styles.keepsake} data-placement={keepsake.placement}
                src={asset(keepsake.image)} alt="" aria-hidden="true" width={keepsake.width} height={keepsake.height}
                unoptimized draggable={false} />}
            </div>
          </div>;
        })}
      </div>
    </div>
    <div className={album.portraitControls}>
      <button type="button" className={album.nextButton} onClick={() => move(-1)} aria-label="Previous photograph"><ArrowLeftIcon size={22} aria-hidden="true" /></button>
      <button type="button" ref={flipRef} className={album.turnButton} onClick={onFlip} aria-pressed={side === "note"}>{side === "note" ? "See the photograph" : "Read the note"}</button>
      <button type="button" className={album.nextButton} onClick={() => move(1)} aria-label="Next photograph"><ArrowRightIcon size={22} aria-hidden="true" /></button>
    </div>
    <p className={album.portraitPosition} aria-live="polite" aria-atomic="true"><span className={album.srOnly}>{selected.title} Photograph </span>{selectedIndex + 1} / {count}</p>
    <p className={album.swipeHint}>Swipe to explore.</p>
    <div className={album.landscapeHint}><DeviceRotateIcon size={36} weight="light" aria-hidden="true" /><p>Better in landscape.<br />Turn your phone to see the whole board.</p></div>
  </div>;
}
