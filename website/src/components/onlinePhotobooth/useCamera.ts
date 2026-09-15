"use client";

import { useCallback, useEffect, useRef, useState, type RefObject } from "react";

type CameraState = { status: "idle" | "requesting" | "ready" | "error"; message: string };
const idle: CameraState = { status: "idle", message: "" };

function cameraMessage(error: unknown) {
  const name = error instanceof DOMException ? error.name : "";
  if (name === "NotAllowedError" || name === "SecurityError") return "Camera access wasn’t allowed. You can try again, or choose photographs from this device.";
  if (name === "NotFoundError" || name === "OverconstrainedError") return "We couldn’t find an available camera. Choose photographs from this device instead.";
  if (name === "NotReadableError") return "Your camera may be in use elsewhere. Close the other camera app and try again, or choose device photographs.";
  return "The camera couldn’t start. Try again, or choose photographs from this device.";
}

export function useCamera(videoRef: RefObject<HTMLVideoElement | null>, onInterrupted: () => void) {
  const [camera, setCamera] = useState<CameraState>(idle);
  const streamRef = useRef<MediaStream | null>(null);
  const requestId = useRef(0);
  const readinessCleanup = useRef<(() => void) | null>(null);

  const release = useCallback(() => {
    requestId.current += 1;
    readinessCleanup.current?.(); readinessCleanup.current = null;
    streamRef.current?.getTracks().forEach((track) => { track.onended = null; track.stop(); });
    streamRef.current = null;
    if (videoRef.current) videoRef.current.srcObject = null;
  }, [videoRef]);

  const stop = useCallback(() => { release(); setCamera(idle); }, [release]);

  const request = useCallback(async () => {
    release();
    if (!navigator.mediaDevices?.getUserMedia || !window.isSecureContext) {
      setCamera({ status: "error", message: "Camera access needs a secure connection and a supported browser. You can still make your strip with photographs from this device." });
      return;
    }
    const id = requestId.current;
    setCamera({ status: "requesting", message: "Allow your camera when your browser asks. You can also choose photographs below." });
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: false, video: { facingMode: "user", width: { ideal: 1640 }, height: { ideal: 1230 }, aspectRatio: { ideal: 4 / 3 } } });
      if (requestId.current !== id || !videoRef.current) { stream.getTracks().forEach((track) => track.stop()); return; }
      streamRef.current = stream;
      const video = videoRef.current;
      video.srcObject = stream;
      stream.getVideoTracks().forEach((track) => {
        track.onended = () => {
          if (requestId.current !== id) return;
          release(); onInterrupted();
          setCamera({ status: "error", message: "The camera connection ended. Your photographs are safe here. Try the camera again or choose a photograph from this device." });
        };
      });
      await new Promise<void>((resolve, reject) => {
        const ready = () => {
          if (video.readyState >= 2 && video.videoWidth > 0 && video.videoHeight > 0) { cleanup(); resolve(); }
        };
        const cleanup = () => { clearTimeout(timer); video.removeEventListener("loadeddata", ready); video.removeEventListener("canplay", ready); readinessCleanup.current = null; };
        const timer = setTimeout(() => { cleanup(); reject(new Error("Camera timed out")); }, 15000);
        readinessCleanup.current = () => { cleanup(); resolve(); };
        video.addEventListener("loadeddata", ready); video.addEventListener("canplay", ready);
        void video.play().then(ready).catch((error) => { cleanup(); reject(error); });
      });
      if (requestId.current === id) setCamera({ status: "ready", message: "" });
    } catch (error) {
      if (requestId.current !== id) return;
      release(); setCamera({ status: "error", message: cameraMessage(error) });
    }
  }, [onInterrupted, release, videoRef]);

  useEffect(() => () => { release(); }, [release]);
  return { ...camera, request, stop };
}
