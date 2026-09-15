import { canOverlap, clampUnit, liftAt, liftTravel } from "./editorialLiftMath";

/** Enhances existing document content; never clones sections or consumes wheel input. */
export function mountEditorialLift(root: HTMLElement) {
  const hero = root.querySelector<HTMLElement>("[data-lift-hero]")!;
  const next = root.querySelector<HTMLElement>("[data-lift-experience]")!;
  const experience = root.querySelector<HTMLElement>("#experience")!;
  const heading = root.querySelector<HTMLElement>("#experience-heading")!;
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
  let enhanced = false;
  let progress = 0;
  let travel = 0;
  let stageHeight = 0;
  let paperDistance = 0;
  let headerHeight = 0;
  let scrollFrame = 0;
  let navigationFrame = 0;
  let disposed = false;

  const rootStart = () => root.getBoundingClientRect().top + window.scrollY - headerHeight;
  const destination = () => enhanced
    ? rootStart() + travel
    : experience.getBoundingClientRect().top + window.scrollY - headerHeight - 16;

  function update() {
    scrollFrame = 0;
    if (!enhanced) return;
    progress = clampUnit((window.scrollY - rootStart()) / travel);
    const frame = liftAt(progress);
    root.style.setProperty("--lift-scale", String(frame.heroScale));
    root.style.setProperty("--lift-copy", String(frame.copy));
    root.style.setProperty("--lift-details", String(frame.details));
    root.style.setProperty("--lift-offset", `${frame.paperOffset * paperDistance}px`);
    root.dataset.liftProgress = progress.toFixed(3);
    hero.inert = progress >= 0.45;
    next.inert = progress < 0.45;
  }
  function queueUpdate() {
    if (!scrollFrame) scrollFrame = window.requestAnimationFrame(update);
  }
  function cancelNavigation() {
    window.cancelAnimationFrame(navigationFrame);
    navigationFrame = 0;
  }
  function measure() {
    if (disposed) return;
    const wasEnhanced = enhanced;
    const wasAtExperience = progress >= 0.45;
    const before = root.getBoundingClientRect();
    const wasInSequence = before.bottom > headerHeight && before.top < window.innerHeight;
    headerHeight = document.querySelector("header")?.getBoundingClientRect().height ?? 73;
    enhanced = canOverlap(window.innerWidth, window.innerHeight - headerHeight, hero.offsetHeight, reduced.matches);
    travel = liftTravel(window.innerWidth, window.innerHeight);
    root.style.setProperty("--lift-travel", `${travel}px`);
    root.style.setProperty("--lift-header", `${headerHeight}px`);
    root.dataset.liftMode = enhanced ? "scroll" : "flow";
    stageHeight = Math.ceil(Math.max(hero.offsetHeight, experience.offsetHeight));
    // A tall phone Experience section must not delay the incoming paper until
    // the final moment. Start the sheet at the bottom of the actual hero.
    paperDistance = hero.offsetHeight;
    root.style.setProperty("--lift-height", `${stageHeight}px`);
    if (!enhanced) {
      hero.inert = false;
      next.inert = false;
      root.removeAttribute("data-lift-progress");
    }
    if (wasEnhanced !== enhanced) {
      cancelNavigation();
      if (wasInSequence && ((wasEnhanced && wasAtExperience) || (enhanced && location.hash === "#experience"))) {
        window.scrollTo({ top: destination(), behavior: "instant" });
      }
    }
    update();
  }
  function arrive() {
    navigationFrame = 0;
    update();
    heading.focus({ preventScroll: true });
  }
  function jump() {
    window.scrollTo({ top: destination(), behavior: "instant" });
    arrive();
  }
  function navigate() {
    cancelNavigation();
    // Record the origin before scrolling, matching native anchor Back behavior.
    if (location.hash !== "#experience") history.pushState(null, "", "#experience");
    if (reduced.matches) { jump(); return; }
    const from = window.scrollY;
    const to = destination();
    const start = performance.now();
    const duration = enhanced ? (window.innerWidth < 768 ? 750 : 950) : 650;
    function advance(now: number) {
      if (disposed) return;
      const amount = clampUnit((now - start) / duration);
      const eased = amount * amount * (3 - 2 * amount);
      window.scrollTo({ top: from + (to - from) * eased, behavior: "instant" });
      update();
      if (amount < 1) navigationFrame = window.requestAnimationFrame(advance);
      else arrive();
    }
    navigationFrame = window.requestAnimationFrame(advance);
  }
  function onClick(event: MouseEvent) {
    const anchor = (event.target as Element | null)?.closest?.("a[href]");
    if (!anchor || anchor.getAttribute("href") !== "#experience") {
      cancelNavigation();
      return;
    }
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.altKey || event.shiftKey) return;
    event.preventDefault();
    navigate();
  }
  function onKey(event: KeyboardEvent) {
    if (["Escape", "ArrowDown", "ArrowUp", "PageDown", "PageUp", "Home", "End", "Tab", " "].includes(event.key)) cancelNavigation();
  }
  function onHash() {
    cancelNavigation();
    if (location.hash === "#experience" && enhanced) {
      window.scrollTo({ top: destination(), behavior: "instant" });
      update();
    }
  }
  function onPreference() { cancelNavigation(); measure(); }
  function onResize() { cancelNavigation(); measure(); }
  const observer = new ResizeObserver(measure);
  observer.observe(hero);
  observer.observe(next);
  document.addEventListener("click", onClick);
  document.addEventListener("keydown", onKey);
  window.addEventListener("wheel", cancelNavigation, { passive: true });
  window.addEventListener("touchstart", cancelNavigation, { passive: true });
  window.addEventListener("scroll", queueUpdate, { passive: true });
  window.addEventListener("resize", onResize);
  window.addEventListener("hashchange", onHash);
  reduced.addEventListener("change", onPreference);
  measure();
  onHash();
  void document.fonts.ready.then(() => { if (!disposed) measure(); });

  return () => {
    disposed = true;
    cancelNavigation();
    window.cancelAnimationFrame(scrollFrame);
    observer.disconnect();
    document.removeEventListener("click", onClick);
    document.removeEventListener("keydown", onKey);
    window.removeEventListener("wheel", cancelNavigation);
    window.removeEventListener("touchstart", cancelNavigation);
    window.removeEventListener("scroll", queueUpdate);
    window.removeEventListener("resize", onResize);
    window.removeEventListener("hashchange", onHash);
    reduced.removeEventListener("change", onPreference);
    hero.inert = false;
    next.inert = false;
    delete root.dataset.liftMode;
    delete root.dataset.liftProgress;
    for (const name of ["height", "travel", "header", "scale", "copy", "details", "offset"]) root.style.removeProperty(`--lift-${name}`);
  };
}
