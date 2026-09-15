import {
  AbsoluteFill,
  Easing,
  Img,
  interpolate,
  staticFile,
  useCurrentFrame,
} from "remotion";
import {cormorantGaramond, manrope} from "../../../shared/fonts";
import {COLORS} from "../../../shared/theme";

type WeekendClubEditorialFrameProps = {
  durationInFrames: number;
  fadeIn?: boolean;
  fadeOut?: boolean;
  photo: string;
  startScale?: number;
  endScale?: number;
};

const SHELL = "generated/designs/weekend-club-background-v2.png";

export const WeekendClubEditorialShell: React.FC<
  React.PropsWithChildren
> = ({children}) => {
  return (
    <AbsoluteFill
      style={{
        backgroundColor: COLORS.warmIvory,
        overflow: "hidden",
      }}
    >
      <Img
        src={staticFile(SHELL)}
        style={{
          position: "absolute",
          inset: 0,
          width: 1080,
          height: 1920,
          objectFit: "cover",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 100,
          top: 180,
          width: 880,
          height: 250,
          color: "#31453F",
          fontFamily: cormorantGaramond,
          fontWeight: 600,
          textAlign: "center",
        }}
      >
        <div
          style={{
            fontSize: 66,
            letterSpacing: 2,
            lineHeight: 0.78,
          }}
        >
          THE
        </div>
        <div
          style={{
            marginTop: 16,
            fontSize: 120,
            letterSpacing: -1.5,
            lineHeight: 0.72,
          }}
        >
          WEEKEND
        </div>
        <div
          style={{
            marginTop: 16,
            fontSize: 120,
            letterSpacing: -1.5,
            lineHeight: 0.72,
          }}
        >
          FINDS CLUB
        </div>
      </div>
      <div
        style={{
          position: "absolute",
          left: 160,
          top: 450,
          width: 760,
          height: 975,
          boxSizing: "border-box",
          padding: "30px 30px 72px",
          backgroundColor: COLORS.offWhite,
          boxShadow: "0 22px 52px rgba(45, 33, 27, 0.18)",
          rotate: "-0.6deg",
          transformOrigin: "50% 50%",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            position: "relative",
            width: "100%",
            height: "100%",
            overflow: "hidden",
          }}
        >
          {children}
        </div>
      </div>
      <div
        style={{
          position: "absolute",
          left: 290,
          top: 1445,
          width: 500,
          height: 120,
          boxSizing: "border-box",
          color: "#31453F",
          textAlign: "center",
        }}
      >
        <div
          style={{
            fontFamily: cormorantGaramond,
            fontSize: 100,
            fontWeight: 600,
            letterSpacing: 2.4,
            lineHeight: 0.82,
          }}
        >
          FOTOHAVN
        </div>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            gap: 14,
            marginTop: 15,
            color: COLORS.mutedBrass,
            fontFamily: manrope,
            fontSize: 28,
            fontWeight: 800,
            letterSpacing: 3.2,
          }}
        >
          <div style={{width: 40, height: 3, backgroundColor: COLORS.mutedBrass}} />
          AT EVIA MALL
          <div style={{width: 40, height: 3, backgroundColor: COLORS.mutedBrass}} />
        </div>
      </div>
    </AbsoluteFill>
  );
};

export const WeekendClubEditorialFrame: React.FC<
  WeekendClubEditorialFrameProps
> = ({
  durationInFrames,
  fadeIn = true,
  fadeOut = false,
  photo,
  startScale = 1,
  endScale = 1.012,
}) => {
  const frame = useCurrentFrame();
  const fadeFrames = 8;
  const fadeInOpacity = fadeIn
    ? interpolate(frame, [0, fadeFrames], [0, 1], {
        extrapolateLeft: "clamp",
        extrapolateRight: "clamp",
        easing: Easing.inOut(Easing.quad),
      })
    : 1;
  const fadeOutOpacity = fadeOut
    ? interpolate(
        frame,
        [durationInFrames - fadeFrames - 1, durationInFrames - 1],
        [1, 0],
        {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
          easing: Easing.inOut(Easing.quad),
        },
      )
    : 1;
  const opacity = Math.min(fadeInOpacity, fadeOutOpacity);
  const scale = interpolate(
    frame,
    [0, Math.max(1, durationInFrames - 1)],
    [startScale, endScale],
    {
      extrapolateLeft: "clamp",
      extrapolateRight: "clamp",
      easing: Easing.inOut(Easing.quad),
    },
  );

  return (
    <Img
      src={staticFile(photo)}
      style={{
        width: "100%",
        height: "100%",
        objectFit: "contain",
        opacity,
        scale,
      }}
    />
  );
};
