import {AbsoluteFill} from "remotion";
import {FullBleedVideo} from "../../../shared/FullBleedVideo";
import {VIDEO} from "../../../shared/theme";

export const PosePrintScene: React.FC = () => {
  return (
    <AbsoluteFill>
      <FullBleedVideo
        src={VIDEO.boothGuest}
        trimBefore={6}
        objectFit="contain"
        startScale={1.01}
        endScale={1.035}
        shade={0.08}
        animationDurationInFrames={90}
      />
    </AbsoluteFill>
  );
};
