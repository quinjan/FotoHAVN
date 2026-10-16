/**
 * Which strip the guest opened, so the gallery row can put focus back on it when the detail closes. The row and the detail
 * are separate views, so the row is gone while the detail shows. Only the opaque id is kept; it is never shown.
 */
let opened: string | null = null;

export function rememberOpenedStrip(id: string): void {
  opened = id;
}

export function rememberedStrip(): string | null {
  return opened;
}

export function forgetOpenedStrip(): void {
  opened = null;
}
