import type { ComponentFixture } from "../fixture.ts";
import { fixtureStrip } from "../shared/fixturePhotographs.ts";
import boxes from "./PrintObject.fixtures.module.css";
import { PrintObject } from "./PrintObject.tsx";

const signature = fixtureStrip("signature-strip", 0);
const panorama = fixtureStrip("panorama-four", 2);
const gingham = fixtureStrip("butter-gingham", 1);
const polka = fixtureStrip("polka-keepsake", 3);
const alt = "Photo Strip from this Event";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "print-object-resting",
    title: "Print object, resting, print-contact shadow",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={boxes.tall}>
        <PrintObject {...signature} alt={alt} />
      </div>
    ),
  },
  {
    id: "print-object-lifted",
    title: "Print object, lifted on Warm Ivory, print shadow",
    surface: "light",
    role: "guest",
    render: () => (
      <div className={boxes.editorial}>
        <div className={boxes.tall}>
          <PrintObject {...signature} alt={alt} shadow="lifted" />
        </div>
      </div>
    ),
  },
  {
    id: "print-object-on-mat",
    title: "Print object on a Cream Paper mat (Template preview)",
    surface: "light",
    role: "guest",
    render: () => (
      <div className={boxes.mat} data-tone="paper">
        <PrintObject {...gingham} alt={alt} />
      </div>
    ),
  },
  {
    id: "print-object-panorama",
    title: "Print object, Panorama Four in its native orientation",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={boxes.wide}>
        <PrintObject {...panorama} alt={alt} />
      </div>
    ),
  },
  {
    id: "print-object-panorama-rotated",
    title: "Print object, Panorama Four turned a quarter counterclockwise",
    surface: "light",
    role: "guest",
    render: () => (
      <div className={boxes.mat} data-tone="paper">
        <PrintObject {...panorama} alt={alt} rotated />
      </div>
    ),
  },
  {
    id: "print-object-table",
    title: "Print objects at a common scale, each Template's real proportion",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={boxes.table}>
        <PrintObject {...signature} alt={alt} fit="scale" />
        <PrintObject {...panorama} alt={alt} fit="scale" />
        <PrintObject {...gingham} alt={alt} fit="scale" />
        <PrintObject {...polka} alt={alt} fit="scale" />
        <PrintObject {...panorama} alt={alt} fit="scale" rotated />
      </div>
    ),
  },
];
