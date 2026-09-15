import {loadFont as loadCormorantGaramond} from "@remotion/google-fonts/CormorantGaramond";
import {loadFont as loadManrope} from "@remotion/google-fonts/Manrope";

export const {fontFamily: cormorantGaramond} = loadCormorantGaramond(
  "normal",
  {
    weights: ["500", "600", "700"],
    subsets: ["latin"],
  },
);

export const {fontFamily: manrope} = loadManrope("normal", {
  weights: ["400", "500", "600", "700", "800"],
  subsets: ["latin"],
});
