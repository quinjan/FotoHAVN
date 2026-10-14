/**
 * Where the page fetches a composed preview from (implementation plan, Images and Presentation: `preview.fotohavn`).
 * The brain names a preview by its key (`<templateId>.<lookId>.r<revision>`); each transport knows how its host serves it:
 * the WPF host answers `https://preview.fotohavn/<key>.png` through WebView2, and the headless host serves the same
 * bytes at `/preview/<key>.png` on the page's origin (or, with `?brain=` on another origin, on that host's origin).
 */

/** The WebView2 virtual host's address for a preview. */
export function webViewPreviewUrl(key: string): string {
  return `https://preview.fotohavn/${encodeURIComponent(key)}.png`;
}

/** The headless host's preview URL under `base` (`/preview/` for the page's own origin). */
export function headlessPreviewUrl(key: string, base = "/preview/"): string {
  return `${base}${encodeURIComponent(key)}.png`;
}

/**
 * The base of the headless host's preview route: the page's own origin, or, when `?brain=` names a bridge on another
 * origin, that bridge's HTTP(S) origin. Throws when `brain` is not a URL (the caller already treats that as no transport).
 */
export function headlessPreviewBase(brain: string | null): string {
  if (brain === null) {
    return "/preview/";
  }
  const url = new URL(brain);
  url.protocol = url.protocol === "wss:" ? "https:" : "http:";
  return `${url.origin}/preview/`;
}
