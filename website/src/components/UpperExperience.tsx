import { ArrowRightIcon } from "@phosphor-icons/react/dist/ssr/ArrowRight";
import Image from "./EventPhoto";
import { withSiteBasePath } from "../../site.config";
import HeroExterior from "./HeroExterior";
import EditorialLift from "./EditorialLift";
import ExperienceReveal from "./ExperienceReveal";
import styles from "./UpperExperience.module.css";

const stories = [
  {
    name: "candid-v2",
    className: styles.privacy,
    alt: "Two friends laughing together against the cream backdrop inside the FOTOHAVN booth.",
    heading: "Room to be yourself.",
    copy: "A private space, away from the crowd.",
    sizes: "(max-width: 767px) calc(100vw - 48px), (min-width: 1600px) 667px, 42vw",
  },
  {
    name: "look-prints",
    className: styles.look,
    alt: "Four FOTOHAVN photostrips showing one guest in color, sepia, rotated monochrome, and high-contrast finishes.",
    heading: "A look that feels like you.",
    copy: "Choose the frame and photographic finish.",
    sizes: "(max-width: 767px) calc(100vw - 48px), (min-width: 1600px) 267px, 17vw",
  },
  {
    name: "keepsake-reunion",
    className: styles.keepsake,
    alt: "Three friends laughing together in a café as they revisit old FOTOHAVN photostrips.",
    heading: "A memory made to last.",
    copy: "A FOTOHAVN moment, printed to last.",
    sizes: "(max-width: 767px) calc(100vw - 48px), (min-width: 1600px) 667px, 42vw",
  },
] as const;

export default function UpperExperience() {
  return (
    <EditorialLift hero={<HeroExterior />}>
      <ExperienceReveal>
        <section
          id="experience"
          className={styles.experience}
          aria-labelledby="experience-heading"
        >
          <div className={styles.introduction}>
            <p className={styles.eyebrow}>THE FOTOHAVN EXPERIENCE</p>
            <h2 id="experience-heading" tabIndex={-1} className={styles.headline}>
              <span>A little privacy.</span>{" "}
              <span>A little <em>possibility.</em></span>
            </h2>
          </div>

          <div className={styles.stories}>
            {stories.map((story) => (
              <figure key={story.name} className={styles.story + " " + story.className}>
                <div className={styles.photograph}>
                  <Image
                    src={withSiteBasePath(`/images/experience-online/${story.name}.webp`)}
                    alt={story.alt}
                    fill
                    loading="eager"
                    sizes={story.sizes}
                  />
                </div>
                <figcaption className={styles.offer} data-experience-reveal>
                  <h3>{story.heading}</h3>
                  <p>{story.copy}</p>
                </figcaption>
              </figure>
            ))}
          </div>

          <div className={styles.invitation}>
            <div className={styles.invitationInner} data-experience-reveal>
              <div className={styles.invitationCopy}>
                <h3>Your next photograph starts here.</h3>
                <p>Step into our online booth. Make a digital strip of your own.</p>
              </div>
              <a className={styles.onlineAction} href={withSiteBasePath("/online")}>
                <span>EXPERIENCE FOTOHAVN ONLINE</span>
                <ArrowRightIcon size={20} weight="light" aria-hidden="true" />
              </a>
            </div>
          </div>
        </section>
      </ExperienceReveal>
    </EditorialLift>
  );
}
