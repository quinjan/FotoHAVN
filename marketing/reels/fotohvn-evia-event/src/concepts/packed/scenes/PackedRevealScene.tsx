import {AbsoluteFill} from "remotion";
import {FullBleedVideo} from "../../../shared/FullBleedVideo";
import {VIDEO} from "../../../shared/theme";

export const PackedRevealScene: React.FC = () => {
  return (
    <AbsoluteFill>
      <FullBleedVideo
        src={VIDEO.boothReveal}
        trimBefore={105}
        startScale={1}
        endScale={1.03}
        shade={0.08}
        animationDurationInFrames={75}
      />
    </AbsoluteFill>
  );
};
