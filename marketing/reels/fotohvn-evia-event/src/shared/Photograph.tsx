import {
  Easing,
  Img,
  interpolate,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";

type PhotographProps = {
  src: string;
  left?: number;
  top?: number;
  width?: number;
  height?: number;
  objectFit?: "cover" | "contain";
  objectPosition?: string;
  startScale?: number;
  endScale?: number;
  animationDurationInFrames?: number;
};

export const Photograph: React.FC<PhotographProps> = ({
  src,
  left = 0,
  top = 0,
  width = 1080,
  height = 1920,
  objectFit = "cover",
  objectPosition = "center",
  startScale = 1,
  endScale = 1.035,
  animationDurationInFrames,
}) => {
  const frame = useCurrentFrame();
  const {durationInFrames} = useVideoConfig();
  const animationDuration = animationDurationInFrames ?? durationInFrames;

  return (
    <Img
      src={staticFile(src)}
      style={{
        position: "absolute",
        left,
        top,
        width,
        height,
        objectFit,
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
  );
};
