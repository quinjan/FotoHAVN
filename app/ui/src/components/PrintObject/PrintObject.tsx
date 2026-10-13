import type { CSSProperties } from "react";
import { cx } from "../shared/cx.ts";
import styles from "./PrintObject.module.css";

/** `contact`: the small `print-contact` shadow of a print resting on a surface. `lifted`: the `print` shadow. */
export type PrintShadow = "contact" | "lifted";

/**
 * How the print takes its size. `contain`: the largest print that fits the parent's box (the parent gives it a
 * definite width and height), centered. `scale`: a size proportional to the image's native pixels, so Photo Strips of
 * different Templates keep their real proportions to each other, like prints laid on a table; the parent sets
 * `--print-scale` in its CSS to the displayed length of 1000 native pixels (default 150px).
 */
export type PrintFit = "contain" | "scale";

export interface PrintObjectProps {
  /** The image URL: a composed Photo Strip, or a thumbnail of one. */
  readonly src: string;
  /** The image's alternative text, e.g. "Photo Strip from this Event". */
  readonly alt: string;
  /** The image's native width in pixels; with `height`, it fixes the print's proportion. */
  readonly width: number;
  /** The image's native height in pixels. */
  readonly height: number;
  /** One shadow per object. Default `contact`. */
  readonly shadow?: PrintShadow;
  /**
   * Turns the print a quarter turn counterclockwise, as the Guest Screen shows Panorama Four's previews. The layout
   * takes the turned box, so a landscape strip occupies a portrait footprint. The image file is unchanged.
   */
  readonly rotated?: boolean;
  /** Default `contain`. */
  readonly fit?: PrintFit;
  readonly className?: string | undefined;
}

/**
 * A Photo Strip as a physical print (brand components.md, Print object): the image at its native proportion on an
 * Off-white border, with the `print-contact` shadow when resting or the `print` shadow when lifted. Use it wherever a
 * Photo Strip is shown: Template and Filters previews, Keepsake, and the Event Gallery.
 */
export function PrintObject({
  src,
  alt,
  width,
  height,
  shadow = "contact",
  rotated = false,
  fit = "contain",
  className,
}: PrintObjectProps) {
  const displayWidth = rotated ? height : width;
  const displayHeight = rotated ? width : height;
  const geometry = {
    "--print-ratio": String(displayWidth / displayHeight),
    "--print-display-width": String(displayWidth),
  } as CSSProperties;
  const print = (
    <div
      className={cx(styles.print, fit === "scale" && className)}
      data-fit={fit}
      data-shadow={shadow}
      data-rotated={rotated || undefined}
      style={geometry}
    >
      <img className={styles.image} src={src} alt={alt} width={width} height={height} draggable={false} />
    </div>
  );
  return fit === "scale" ? print : <div className={cx(styles.fit, className)}>{print}</div>;
}
