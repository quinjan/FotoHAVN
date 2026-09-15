import {
  AbsoluteFill,
  Easing,
  Img,
  interpolate,
  Sequence,
  staticFile,
  useCurrentFrame,
} from "remotion";
import {COLORS, PHOTO} from "../../../shared/theme";

type PhotoBeatProps = {
  src: string;
  durationInFrames: number;
  startScale: number;
  endScale: number;
};

const PhotoBeat: React.FC<PhotoBeatProps> = ({
  src,
  durationInFrames,
  startScale,
  endScale,
}) => {
  const frame = useCurrentFrame();

  return (
    <AbsoluteFill style={{backgroundColor: COLORS.ebony}}>
      <div
        style={{
          position: "absolute",
          left: 58,
          top: 150,
          width: 964,
          height: 1450,
          padding: 20,
          boxSizing: "border-box",
          backgroundColor: COLORS.offWhite,
          opacity: interpolate(
            frame,
            [0, 4, durationInFrames - 5, durationInFrames - 1],
            [0, 1, 1, 0],
            {
              extrapolateLeft: "clamp",
              extrapolateRight: "clamp",
              easing: Easing.inOut(Easing.quad),
            },
          ),
          scale: interpolate(frame, [0, durationInFrames - 1], [startScale, endScale], {
            extrapolateLeft: "clamp",
            extrapolateRight: "clamp",
            easing: Easing.inOut(Easing.quad),
          }),
        }}
      >
        <Img
          src={staticFile(src)}
          style={{width: "100%", height: "100%", objectFit: "contain"}}
        />
      </div>
    </AbsoluteFill>
  );
};

export const StepInsideScene: React.FC = () => {
  return (
    <AbsoluteFill style={{backgroundColor: COLORS.ebony, overflow: "hidden"}}>
      <Sequence durationInFrames={24}>
        <PhotoBeat
          src={PHOTO.packedAtrium}
          durationInFrames={24}
          startScale={1.015}
          endScale={1.035}
        />
      </Sequence>
      <Sequence from={20} durationInFrames={24}>
        <PhotoBeat
          src={PHOTO.boothUniqlo}
          durationInFrames={24}
          startScale={1.01}
          endScale={1.03}
        />
      </Sequence>
      <Sequence from={40} durationInFrames={26}>
        <PhotoBeat
          src={PHOTO.boothQueue}
          durationInFrames={26}
          startScale={1.01}
          endScale={1.025}
        />
      </Sequence>
    </AbsoluteFill>
  );
};
