import {
  AbsoluteFill,
  Easing,
  interpolate,
  spring,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import {FullBleedVideo} from "../../../shared/FullBleedVideo";
import {cormorantGaramond, manrope} from "../../../shared/fonts";
import {COLORS, VIDEO} from "../../../shared/theme";

export const MallFullScene: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const enter = spring({
    frame,
    fps,
    config: {damping: 190},
    durationInFrames: 26,
  });

  return (
    <AbsoluteFill>
      <FullBleedVideo
        src={VIDEO.crowdElevated}
        trimBefore={15}
        startScale={1.01}
        endScale={1.025}
        shade={0.02}
        animationDurationInFrames={81}
      />
      <div
        style={{
          position: "absolute",
          left: 0,
          top: 1090,
          width: 1080,
          height: 510,
          padding: "58px 100px 64px",
          boxSizing: "border-box",
          backgroundColor: "rgba(243, 235, 221, 0.96)",
          color: COLORS.ebony,
          borderTop: `8px solid ${COLORS.mutedBrass}`,
          opacity: interpolate(frame, [0, 10, 70, 80], [0.92, 1, 1, 0], {
            extrapolateLeft: "clamp",
            extrapolateRight: "clamp",
            easing: Easing.inOut(Easing.quad),
          }),
          translate: `0 ${interpolate(enter, [0, 1], [18, 0])}px`,
        }}
      >
        <div
          style={{
            fontFamily: manrope,
            fontSize: 34,
            fontWeight: 700,
            letterSpacing: 4.8,
            textTransform: "uppercase",
          }}
        >
          Final day • Evia Mall
        </div>
        <div
          style={{
            marginTop: 30,
            fontFamily: cormorantGaramond,
            fontSize: 108,
            fontWeight: 700,
            lineHeight: 0.86,
            letterSpacing: -2.8,
          }}
        >
          THE WEEKEND
          <br />
          FINDS CLUB
        </div>
      </div>
    </AbsoluteFill>
  );
};
