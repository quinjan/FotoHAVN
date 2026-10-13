/** "1 second", "3 seconds": the count and its unit for announcements and messages. */
export function secondsText(seconds: number): string {
  return seconds === 1 ? "1 second" : `${String(seconds)} seconds`;
}
