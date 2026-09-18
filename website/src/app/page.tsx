import { Suspense } from "react";
import ClosingExperience from "@/components/ClosingExperience";
import MiddleExperience from "@/components/MiddleExperience";
import SiteChrome from "@/components/SiteChrome";
import UpperExperience from "@/components/UpperExperience";
import BoothExplorer from "@/components/BoothExplorer";
import AnalyticsConsentPrototype from "@/components/AnalyticsConsentPrototype";

export default function Home() {
  return (
    <div id="top">
      <div id="top-sentinel" aria-hidden="true" />
      <a className="skipLink" href="#main-content">
        Skip to main content
      </a>
      <SiteChrome />
      <main id="main-content" className="siteMain">
        <UpperExperience />
        <BoothExplorer />
        <MiddleExperience />
        <ClosingExperience />
      </main>
      {process.env.NODE_ENV !== "production" && (
        <Suspense fallback={null}>
          <AnalyticsConsentPrototype />
        </Suspense>
      )}
    </div>
  );
}
