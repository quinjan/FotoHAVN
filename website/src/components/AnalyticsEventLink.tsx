"use client";

import type { AnchorHTMLAttributes, ReactNode } from "react";
import { emitAnalyticsEvent } from "@/analytics/client";
import type { AnalyticsEventName } from "@/analytics/contracts";

export default function AnalyticsEventLink({
  event,
  children,
  onClick,
  ...props
}: AnchorHTMLAttributes<HTMLAnchorElement> & { event: AnalyticsEventName; children: ReactNode }) {
  return (
    <a
      {...props}
      onClick={(clickEvent) => {
        emitAnalyticsEvent(event);
        onClick?.(clickEvent);
      }}
    >
      {children}
    </a>
  );
}
