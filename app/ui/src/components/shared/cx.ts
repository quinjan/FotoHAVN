/** Joins class names, skipping empty values: `cx(styles.a, active && styles.b, className)`. */
export function cx(...names: readonly (string | false | null | undefined)[]): string {
  return names.filter((name): name is string => typeof name === "string" && name !== "").join(" ");
}
