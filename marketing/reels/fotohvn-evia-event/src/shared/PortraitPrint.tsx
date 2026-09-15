import {
  AbsoluteFill,
  Easing,
  Img,
  interpolate,
  staticFile,
  useCurrentFrame,
} from "remotion";
import {cormorantGaramond, manrope} from "./fonts";
import {COLORS} from "./theme";

type PortraitPrintProps = {
  src: string;
  label?: string;
  rotation?: number;
  compact?: boolean;
  fadeFrames?: number;
  shotDurationInFrames: number;
};

export const PortraitPrint: React.FC<PortraitPrintProps> = ({
  src,
  label,
  rotation = 0,
  compact = false,
  fadeFrames = 4,
  shotDurationInFrames,
}) => {
  const frame = useCurrentFrame();
  const paperTop = compact ? 330 : 300;
  const paperWidth = compact ? 900 : 880;
  const paperHeight = compact ? 1170 : 1270;
  const paperLeft = (1080 - paperWidth) / 2;

  return (
    <AbsoluteFill style={{backgroundColor: COLORS.darkWalnut}}>
      <div
        style={{
          position: "absolute",
          left: paperLeft,
          top: paperTop,
          width: paperWidth,
          height: paperHeight,
          padding: 28,
          boxSizing: "border-box",
          backgroundColor: COLORS.warmIvory,
          boxShadow: "0 24px 60px rgba(10, 8, 7, 0.24)",
          rotate: `${rotation}deg`,
          scale: interpolate(
            frame,
            [0, Math.max(1, shotDurationInFrames - 1)],
            [1.01, 1.045],
            {
              extrapolateLeft: "clamp",
              extrapolateRight: "clamp",
              easing: Easing.inOut(Easing.quad),
            },
          ),
          opacity: interpolate(
            frame,
            [
              0,
              fadeFrames,
              shotDurationInFrames - fadeFrames - 1,
              shotDurationInFrames - 1,
            ],
            [0, 1, 1, 0],
            {
              extrapolateLeft: "clamp",
              extrapolateRight: "clamp",
            },
          ),
        }}
      >
        <Img
          src={staticFile(src)}
          style={{width: "100%", height: "100%", objectFit: "contain"}}
        />
      </div>
      {label ? (
        <div
          style={{
            position: "absolute",
            left: 100,
            top: 210,
            width: 880,
            color: COLORS.offWhite,
            fontFamily: label === "PHOTOGRAPHS TO KEEP." ? cormorantGaramond : manrope,
            fontSize: label === "PHOTOGRAPHS TO KEEP." ? 64 : 48,
            fontWeight: 700,
            lineHeight: 0.96,
            letterSpacing: label === "PHOTOGRAPHS TO KEEP." ? -1.2 : 1.5,
            textAlign: "center",
            textTransform: "uppercase",
            whiteSpace: "nowrap",
            opacity: interpolate(
              frame,
              [
                2,
                fadeFrames + 3,
                shotDurationInFrames - fadeFrames - 1,
                shotDurationInFrames - 1,
              ],
              [0, 1, 1, 0],
              {
                extrapolateLeft: "clamp",
                extrapolateRight: "clamp",
                easing: Easing.inOut(Easing.quad),
              },
            ),
          }}
        >
          {label}
        </div>
      ) : null}
    </AbsoluteFill>
  );
};
