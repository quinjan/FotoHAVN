/**
 * Where the operator was in the gallery list when a Guest Cycle's detail opened. The list and the detail are separate
 * surfaces, so the list is gone while the detail shows; this remembers the Guest Cycle and its position so that focus
 * can go back to it (or to what took its place, after a delete) when the list opens again.
 */
export interface RememberedFocus {
  readonly eventId: string;
  readonly id: string;
  readonly index: number;
}

let remembered: RememberedFocus | null = null;

export function rememberOpened(focus: RememberedFocus): void {
  remembered = focus;
}

export function rememberedFocus(): RememberedFocus | null {
  return remembered;
}

export function forgetOpened(): void {
  remembered = null;
}
