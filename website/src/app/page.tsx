import ClosingExperience from "@/components/ClosingExperience";
import MiddleExperience from "@/components/MiddleExperience";
import SiteChrome from "@/components/SiteChrome";
import UpperExperience from "@/components/UpperExperience";
import WebsiteIntroPrototype from "@/components/WebsiteIntroPrototype";

export default async function Home({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const requestedVariant = (await searchParams).variant;
  const initialVariant = requestedVariant === "B" ? "B" : "A";

  return (
    <>
      <WebsiteIntroPrototype initialVariant={initialVariant} />
      <div id="site-content">
        <div id="top">
          <a className="skipLink" href="#main-content">
            Skip to main content
          </a>
          <SiteChrome />
          <main
            id="main-content"
            className="overflow-x-hidden w-full max-w-full"
          >
            <UpperExperience />
            <MiddleExperience />
            <ClosingExperience />
          </main>
        </div>
      </div>
    </>
  );
}
