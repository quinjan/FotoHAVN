import type { RefObject } from "react";
import { useEffect, useState } from "react";
import { coverCrop } from "./presets";
import styles from "./OnlinePhotobooth.module.css";

// Match normalization to 4:3, then the selected template's center crop.
export function cameraGuide(sourceWidth: number, sourceHeight: number, viewWidth: number, viewHeight: number, ratio: number) {
  if (![sourceWidth, sourceHeight, viewWidth, viewHeight, ratio].every((value) => value > 0)) return null;
  const source = coverCrop(sourceWidth, sourceHeight);
  const width = Math.min(source.width, source.height * ratio);
  const height = Math.min(source.height, source.width / ratio);
  const scale = Math.min(viewWidth / sourceWidth, viewHeight / sourceHeight);
  return { left: (viewWidth - width * scale) / 2, top: (viewHeight - height * scale) / 2, width: width * scale, height: height * scale };
}

export function CameraFrameGuide({ videoRef, ratio, active }: { videoRef: RefObject<HTMLVideoElement | null>; ratio: number; active: boolean }) {
  const [rect, setRect] = useState<ReturnType<typeof cameraGuide>>(null);
  useEffect(() => {
    const video = videoRef.current;
    if (!active || !video) return;
    const update = () => setRect(cameraGuide(video.videoWidth, video.videoHeight, video.clientWidth, video.clientHeight, ratio));
    const observer = new ResizeObserver(update);
    observer.observe(video); video.addEventListener("loadedmetadata", update);
    return () => { observer.disconnect(); video.removeEventListener("loadedmetadata", update); };
  }, [videoRef, ratio, active]);
  return active && rect ? <div className={styles.cameraFrameGuide} style={rect} aria-hidden="true" /> : null;
}
