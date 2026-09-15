"use client";

import Image, { type ImageLoaderProps, type ImageProps } from "next/image";

// These photo sizes are prepared before deployment, including generated hero art.
// Keeping photo delivery static avoids a cold image-resize request on first visit.
function eventPhotoLoader({ src, width }: ImageLoaderProps) {
  return src.replace(/\.webp$/, `-${width}w.webp`);
}

export default function EventPhoto({
  alt,
  ...props
}: Omit<ImageProps, "loader" | "src"> & { src: string }) {
  return <Image {...props} alt={alt} loader={eventPhotoLoader} />;
}
