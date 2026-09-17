"use client";

import { useEffect, useId, useLayoutEffect, useReducer, useRef, type CSSProperties } from "react";
import GuestAlbumCarousel from "./GuestAlbumCarousel";
import StaticImage from "next/image";
import { ArrowsOutIcon } from "@phosphor-icons/react/dist/csr/ArrowsOut";
import { XIcon } from "@phosphor-icons/react/dist/csr/X";
import { ArrowClockwiseIcon } from "@phosphor-icons/react/dist/csr/ArrowClockwise";
import { ArrowLeftIcon } from "@phosphor-icons/react/dist/csr/ArrowLeft";
import { ArrowRightIcon } from "@phosphor-icons/react/dist/csr/ArrowRight";
import { withSiteBasePath } from "../../site.config";
import GuestBoardPhoto from "./GuestBoardPhoto";
import { guestPhotographs, guestBoardReducer, initialBoardState } from "./guestBoardData";
import { animateLift, liftTransform, type Rect } from "./guestBoardMotion";
import styles from "./GuestAlbum.module.css";

const asset = (name: string) => withSiteBasePath(`/images/guest-board/${name}`);
const reducedMotion = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;
function visibleFocusTarget(...candidates: (HTMLElement | null)[]) {
  return candidates.find(element => element?.isConnected && element.getClientRects().length > 0);
}
const boardKeepsakes = [
  { id: "teddy-charm", image: "teddy-charm.webp", width: 480, height: 720 },
  { id: "bunny-charm", image: "bunny-charm.webp", width: 480, height: 720 },
  { id: "ribbon-keepsake", image: "ribbon-keepsake.webp", width: 480, height: 525 },
  { id: "ticket-keepsake", image: "ticket-keepsake.webp", width: 480, height: 285 },
  { id: "ribbon-lower", image: "ribbon-keepsake.webp", width: 480, height: 525 },
  { id: "ticket-upper", image: "ticket-keepsake.webp", width: 480, height: 285 },
];

export default function GuestAlbum() {
  const [state, dispatch] = useReducer(guestBoardReducer, initialBoardState);
  const dialog = useRef<HTMLDialogElement>(null);
  const sheet = useRef<HTMLDivElement>(null);
  const closePhotoButton = useRef<HTMLButtonElement>(null);
  const fullscreenButton = useRef<HTMLButtonElement>(null);
  const portraitFlipButton = useRef<HTMLButtonElement>(null);
  const closeBoardButton = useRef<HTMLButtonElement>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);
  const returningFocus = useRef<HTMLButtonElement | null>(null);
  const origin = useRef<Rect | null>(null);
  const tilt = useRef(0);
  const animation = useRef<Animation | null>(null);
  const closing = useRef(false);
  const mounted = useRef(true);
  const focusFrame = useRef(0);
  const photoId = useId();
  const boardId = useId();
  const selected = state.index === null ? null : guestPhotographs[state.index];
  const isOpen = state.fullscreen || selected !== null;


  useEffect(() => {
    mounted.current = true;
    const preference = window.matchMedia("(prefers-reduced-motion: reduce)");
    const stop = () => { if (preference.matches) animation.current?.finish(); };
    preference.addEventListener("change", stop);
    const element = dialog.current;
    return () => {
      mounted.current = false;
      animation.current?.cancel();
      cancelAnimationFrame(focusFrame.current);
      preference.removeEventListener("change", stop);
      element?.close();
    };
  }, []);

  useEffect(() => {
    if (!isOpen) return;
    const previous = document.documentElement.style.overflow;
    document.documentElement.style.overflow = "hidden";
    return () => { document.documentElement.style.overflow = previous; };
  }, [isOpen]);

  useLayoutEffect(() => {
    if (state.fullscreen) closeBoardButton.current?.focus({ preventScroll: true });
  }, [state.fullscreen]);

  useLayoutEffect(() => {
    if (state.index === null) {
      // Restore only after React has removed the viewer and cleared board inertness.
      if (returningFocus.current) visibleFocusTarget(returningFocus.current, closeBoardButton.current,
        portraitFlipButton.current, fullscreenButton.current)?.focus({ preventScroll: true });
      returningFocus.current = null;
      return;
    }
    if (!sheet.current) return;
    animation.current?.cancel();
    animation.current = animateLift(sheet.current, origin.current, tilt.current, reducedMotion());
    origin.current = null;
    closePhotoButton.current?.focus({ preventScroll: true });
  }, [state.index]);

  function openPhoto(index: number, button: HTMLButtonElement) {
    if (closing.current) return;
    trigger.current = button;
    origin.current = button.getBoundingClientRect();
    tilt.current = guestPhotographs[index].tilt;
    dispatch({ type: "open", index });
    if (!dialog.current?.open) dialog.current?.showModal();
  }

  function focusLater(element: HTMLElement | null) {
    cancelAnimationFrame(focusFrame.current);
    focusFrame.current = requestAnimationFrame(() => visibleFocusTarget(element,
      portraitFlipButton.current, fullscreenButton.current)?.focus({ preventScroll: true }));
  }

  function closePhoto() {
    if (closing.current) return;
    closing.current = true;
    animation.current?.cancel();
    const finish = () => {
      if (!mounted.current) return;
      closing.current = false;
      returningFocus.current = trigger.current;
      dispatch({ type: "close-photo" });
      if (!state.fullscreen) dialog.current?.close();
    };
    const destination = trigger.current?.getBoundingClientRect();
    if (sheet.current && destination && destination.width > 0 && destination.height > 0 && !reducedMotion()) {
      animation.current = sheet.current.animate([
        { transform: "none", opacity: 1 },
        { transform: liftTransform(destination, sheet.current.getBoundingClientRect(), tilt.current), opacity: 0.2 },
      ], { duration: 360, easing: "cubic-bezier(.4,0,.6,1)", fill: "forwards" });
      void animation.current.finished.then(finish, () => {});
    } else finish();
  }

  function openBoard() {
    dispatch({ type: "board" });
    const element = dialog.current;
    if (!element) return;
    if (!element.open) element.showModal();
  }

  function exitBoard() {
    animation.current?.cancel();
    closing.current = false;
    dialog.current?.close();
    dispatch({ type: "exit" });
    focusLater(fullscreenButton.current);
  }

  function nextPhoto(direction: number) {
    if (closing.current) return;
    origin.current = null;
    dispatch({ type: "next", direction });
  }

  function board(fullscreen: boolean) {
    return <div className={styles.metalBoard}
      style={{ borderImageSource: `url("${asset("metal-board.webp")}")` }}
      data-guest-board={fullscreen ? "fullscreen" : "inline"}>
      <ul className={styles.prints} aria-label="Customer photographs and Photo Strips">
        {guestPhotographs.map((photo, index) => <li key={photo.id} data-print={photo.id}
          className={photo.kind === "strip" ? styles.stripItem : styles.portraitItem}
          style={{ "--tilt": `${photo.tilt}deg` } as CSSProperties}>
          <button type="button" className={styles.print} data-photo-id={photo.id}
            aria-haspopup="dialog" aria-label={`Open photograph: ${photo.title}`}
            onClick={event => openPhoto(index, event.currentTarget)}>
            <StaticImage src={asset("silver-clip-small.webp")} alt="" aria-hidden="true"
              width={28} height={38} unoptimized className={styles.clip} />
            <span className={styles.printImage}>
              <GuestBoardPhoto photo={photo} sizes={photo.kind === "strip" ? "(max-width: 600px) 22vw, 110px" : "(max-width: 600px) 46vw, (max-width: 1400px) 18vw, 240px"} />
            </span>
            <span className={styles.printLabel}>{photo.kind === "strip" ? "A little moment" : photo.title}</span>
            <span className={styles.openHint}>Take a closer look</span>
          </button>
        </li>)}
        {boardKeepsakes.map(keepsake => <li key={keepsake.id} className={styles.keepsake}
          data-keepsake={keepsake.id} aria-hidden="true">
          <StaticImage src={asset(keepsake.image)} alt="" unoptimized
            width={keepsake.width} height={keepsake.height} draggable={false} />
        </li>)}
      </ul>
    </div>;
  }

  return <section id="guest-album" className={styles.album} aria-labelledby="album-heading">
    <div className={styles.intro}>
      <p className={styles.eyebrow}>ON THE OTHER SIDE</p>
      <h2 id="album-heading">Look at <em>you.</em></h2>
      <p className={styles.invitation}>Every photograph has another side.</p>
    </div>
    <GuestAlbumCarousel selectedIndex={state.selectedIndex} side={state.side}
      onSelect={index => dispatch({ type: "portrait-next", direction: index - state.selectedIndex })}
      onFlip={() => dispatch({ type: "flip" })} flipRef={portraitFlipButton} />
    <div className={styles.boardToolbar}>
      <p>Choose any photograph. There’s a little note behind each one.</p>
      <button ref={fullscreenButton} type="button" className={styles.textButton} onClick={openBoard}>
        <ArrowsOutIcon size={19} aria-hidden="true" /> View board fullscreen
      </button>
    </div>
    {board(false)}
    <div className={styles.boardFooter}>
      <p>GOOD COMPANY. EVEN BETTER KEEPSAKES.</p>
      <span>{guestPhotographs.length} photographs, all yours to explore.</span>
    </div>

    <dialog ref={dialog} className={styles.modal} data-fullscreen={state.fullscreen}
      aria-labelledby={selected ? photoId : boardId}
      onCancel={event => { event.preventDefault(); if (selected) closePhoto(); else exitBoard(); }}
      onKeyDown={event => {
        if (!selected || !["ArrowLeft", "ArrowRight"].includes(event.key)) return;
        event.preventDefault(); nextPhoto(event.key === "ArrowLeft" ? -1 : 1);
      }}>
      {state.fullscreen && <div className={styles.fullBoard} inert={!!selected}>
        <div className={styles.fullToolbar}>
          <div><h2 id={boardId}>Look at <em>you.</em></h2><p>Choose any print to lift it from the board.</p></div>
          <button ref={closeBoardButton} type="button" className={styles.textButton} onClick={exitBoard}>
            <XIcon size={20} aria-hidden="true" /> Close fullscreen
          </button>
        </div>
        {board(true)}
      </div>}
      {selected && <div className={styles.photoLayer} onClick={event => {
        if (event.target === event.currentTarget) closePhoto();
      }}>
        <div className={styles.viewer}>
          <button ref={closePhotoButton} type="button" className={styles.closePhoto}
            onClick={closePhoto} aria-label="Return photograph to the board">
            <XIcon size={18} aria-hidden="true" /> Back to the board
          </button>
          <div ref={sheet} className={styles.sheet} data-viewed-photo={selected.id}>
            <div className={styles.turningSheet} data-side={state.side}>
              <div className={styles.front} aria-hidden={state.side !== "photo"} inert={state.side !== "photo"}>
                <div className={styles.fullPhoto} data-kind={selected.kind}>
                  <GuestBoardPhoto key={selected.id} photo={selected} sizes="(max-width: 767px) 80vw, 440px" priority />
                </div>
                <h3 id={state.side === "photo" ? photoId : undefined}>{selected.title}</h3>
                <p className={styles.frontCaption}>{selected.kind === "strip" ? "An original FOTOHAVN Photo Strip." : "From the FOTOHAVN guest album at Evia."}</p>
              </div>
              <div className={styles.back} aria-hidden={state.side !== "note"} inert={state.side !== "note"}>
                <div className={styles.notePhoto} data-kind={selected.kind}>
                  <GuestBoardPhoto key={selected.id} photo={selected} sizes="280px" priority />
                </div>
                <p className={styles.noteLabel}>{selected.testimonial ? "IN THEIR WORDS" : "FROM THE FOTOHAVN ALBUM"}</p>
                <h3 id={state.side === "note" ? photoId : undefined}>{selected.title}</h3>
                {selected.testimonial
                  ? <><blockquote>{selected.testimonial.quote}</blockquote><p>{selected.testimonial.attribution}</p></>
                  : <><p className={styles.noteText}>{selected.note}</p><span className={styles.captionCredit}>An editorial note from FOTOHAVN.</span></>}
              </div>
            </div>
          </div>
          <div className={styles.viewerControls}>
            <button type="button" className={styles.nextButton} aria-label="Previous photograph" onClick={() => nextPhoto(-1)}>
              <ArrowLeftIcon size={22} aria-hidden="true" />
            </button>
            <button type="button" className={styles.turnButton} onClick={() => {
              if (!closing.current) dispatch({ type: "flip" });
            }} aria-pressed={state.side === "note"}>
              <ArrowClockwiseIcon size={20} aria-hidden="true" />
              {state.side === "photo" ? "Read the note" : "See the photograph"}
            </button>
            <button type="button" className={styles.nextButton} aria-label="Next photograph" onClick={() => nextPhoto(1)}>
              <ArrowRightIcon size={22} aria-hidden="true" />
            </button>
          </div>
          <p className={styles.position} aria-live="polite">Photograph {(state.index ?? 0) + 1} of {guestPhotographs.length}</p>
        </div>
      </div>}
    </dialog>
  </section>;
}
