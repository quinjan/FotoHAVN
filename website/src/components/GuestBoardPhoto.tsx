"use client";

import { useState } from "react";
import Image from "./EventPhoto";
import { withSiteBasePath } from "../../site.config";
import type { GuestPhotograph } from "./guestBoardData";
import styles from "./GuestAlbum.module.css";

export default function GuestBoardPhoto({ photo, sizes, priority = false }: {
  photo: GuestPhotograph; sizes: string; priority?: boolean;
}) {
  const [failed, setFailed] = useState(false);
  return <>
    <Image src={withSiteBasePath(photo.image)} alt={photo.alt} fill sizes={sizes}
      draggable={false} loading={priority ? "eager" : "lazy"}
      className={failed ? styles.failedPhoto : undefined} onError={() => setFailed(true)} />
    {failed && <span className={styles.imageError}>This photograph couldn’t load. Its note is still here.</span>}
  </>;
}
