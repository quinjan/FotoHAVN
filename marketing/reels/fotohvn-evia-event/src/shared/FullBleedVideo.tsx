import {Video} from "@remotion/media";
import {
  AbsoluteFill,
  Easing,
  interpolate,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";

type FullBleedVideoProps = {
  src: string;
  trimBefore?: number;
  objectFit?: "cover" | "contain";
  objectPosition?: string;
  startScale?: number;
  endScale?: number;
  shade?: number;
  animationDurationInFrames?: number;
  cropLeft?: number;
  cropRight?: number;
};

export const FullBleedVideo: React.FC<FullBleedVideoProps> = ({
  src,
  trimBefore = 0,
  objectFit = "cover",
  objectPosition = "center",
  startScale = 1,
  endScale = 1.035,
  shade = 0,
  animationDurationInFrames,
  cropLeft = 0,
  cropRight = 0,
}) => {
  const frame = useCurrentFrame();
  const {durationInFrames} = useVideoConfig();
  const animationDuration = animationDurationInFrames ?? durationInFrames;

  return (
    <AbsoluteFill style={{overflow: "hidden", backgroundColor: "#1E1A17"}}>
      <Video
        src={staticFile(src)}
        trimBefore={trimBefore}
        volume={0}
        objectFit={objectFit}
        cropLeft={cropLeft}
        cropRight={cropRight}
        style={{
          width: "100%",
          height: "100%",
          objectPosition,
          scale: interpolate(
            frame,
            [0, Math.max(1, animationDuration - 1)],
            [startScale, endScale],
            {
              extrapolateLeft: "clamp",
              extrapolateRight: "clamp",
              easing: Easing.inOut(Easing.quad),
            },
          ),
        }}
      />
      {shade > 0 ? (
        <AbsoluteFill style={{backgroundColor: `rgba(30, 26, 23, ${shade})`}} />
      ) : null}
    </AbsoluteFill>
  );
};
