import {
  AbsoluteFill,
  Easing,
  Img,
  interpolate,
  spring,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import {cormorantGaramond, manrope} from "./fonts";
import {COLORS} from "./theme";

type FinalCallToActionProps = {
  photo: string;
  variant: "packed" | "little-room";
};

export const FinalCallToAction: React.FC<FinalCallToActionProps> = ({
  photo,
  variant,
}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const enter = spring({
    frame,
    fps,
    config: {damping: 180},
    durationInFrames: 24,
  });

  return (
    <AbsoluteFill style={{backgroundColor: COLORS.ebony, overflow: "hidden"}}>
      <Img
        src={staticFile(photo)}
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 1080,
          height: 1440,
          objectFit: "contain",
          scale: interpolate(frame, [0, 89], [1.015, 1.045], {
            extrapolateLeft: "clamp",
            extrapolateRight: "clamp",
            easing: Easing.inOut(Easing.quad),
          }),
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 0,
          top: 800,
          width: 1080,
          height: 800,
          padding: "64px 100px 58px",
          boxSizing: "border-box",
          backgroundColor:
            variant === "packed"
              ? "rgba(30, 26, 23, 0.94)"
              : "rgba(243, 235, 221, 0.95)",
          color: variant === "packed" ? COLORS.offWhite : COLORS.ebony,
          borderTop: `8px solid ${COLORS.mutedBrass}`,
          opacity: enter,
          translate: `0 ${interpolate(enter, [0, 1], [44, 0])}px`,
        }}
      >
        <div
          style={{
            fontFamily: cormorantGaramond,
            fontSize: 92,
            fontWeight: 700,
            lineHeight: 0.92,
            letterSpacing: -1.8,
            textAlign: "center",
          }}
        >
          VISIT FOTOHVN
          <br />
          TODAY
        </div>
        <div
          style={{
            marginTop: 52,
            fontFamily: manrope,
            fontSize: 44,
            fontWeight: 700,
            lineHeight: 1.28,
            letterSpacing: 0.4,
            textAlign: "center",
          }}
        >
          EVIA MALL • ATRIUM CENTER
          <br />
          IN FRONT OF UNIQLO
        </div>
        <div
          style={{
            marginTop: 44,
            paddingTop: 36,
            borderTop: `2px solid ${COLORS.mutedBrass}`,
            fontFamily: manrope,
            fontSize: 52,
            fontWeight: 800,
            letterSpacing: 1.4,
            textAlign: "center",
          }}
        >
          HERE UNTIL 10 PM
        </div>
      </div>
    </AbsoluteFill>
  );
};
