import ClosingExperience from "@/components/ClosingExperience";
import MiddleExperience from "@/components/MiddleExperience";
import SiteChrome from "@/components/SiteChrome";
import UpperExperience from "@/components/UpperExperience";

export default function Home() {
  return (
    <div id="top">
      <a className="skipLink" href="#main-content">
        Skip to main content
      </a>
      <SiteChrome />
      <main id="main-content">
        <UpperExperience />
        <MiddleExperience />
        <ClosingExperience />
      </main>
    </div>
  );
}
