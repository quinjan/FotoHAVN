import {AbsoluteFill} from "remotion";
import {FullBleedVideo} from "../../../shared/FullBleedVideo";
import {VIDEO} from "../../../shared/theme";

export const MiddleOfItScene: React.FC = () => {
  return (
    <AbsoluteFill>
      <FullBleedVideo
        src={VIDEO.boothReveal}
        trimBefore={90}
        startScale={1}
        endScale={1.025}
        shade={0.04}
        animationDurationInFrames={108}
      />
    </AbsoluteFill>
  );
};
