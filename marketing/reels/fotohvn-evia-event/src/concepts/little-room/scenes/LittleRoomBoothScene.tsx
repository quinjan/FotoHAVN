import {
  AbsoluteFill,
  Easing,
  interpolate,
  Sequence,
  useCurrentFrame,
} from "remotion";
import {FullBleedVideo} from "../../../shared/FullBleedVideo";
import {Photograph} from "../../../shared/Photograph";
import {COLORS, PHOTO, VIDEO} from "../../../shared/theme";

const BoothExteriorBeat: React.FC = () => {
  const frame = useCurrentFrame();

  return (
    <AbsoluteFill
      style={{
        backgroundColor: COLORS.warmIvory,
        overflow: "hidden",
        opacity: interpolate(frame, [0, 20, 29], [1, 1, 0], {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
          easing: Easing.inOut(Easing.quad),
        }),
      }}
    >
      <div
        style={{
          position: "absolute",
          left: 78,
          top: 378,
          width: 924,
          height: 1218,
          backgroundColor: COLORS.offWhite,
          boxShadow: "0 22px 54px rgba(45, 33, 27, 0.16)",
        }}
      />
      <Photograph
        src={PHOTO.cleanBooth}
        left={100}
        top={400}
        width={880}
        height={1173}
        objectFit="contain"
        startScale={1.01}
        endScale={1.028}
        animationDurationInFrames={30}
      />
    </AbsoluteFill>
  );
};

const BoothExperienceBeat: React.FC = () => {
  const frame = useCurrentFrame();

  return (
    <AbsoluteFill
      style={{
        opacity: interpolate(frame, [0, 9], [0, 1], {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
          easing: Easing.inOut(Easing.quad),
        }),
      }}
    >
      <FullBleedVideo
        src={VIDEO.boothGuest}
        trimBefore={6}
        objectFit="contain"
        startScale={1.005}
        endScale={1.025}
        shade={0.02}
        animationDurationInFrames={78}
      />
    </AbsoluteFill>
  );
};

export const LittleRoomBoothScene: React.FC = () => {
  return (
    <AbsoluteFill style={{backgroundColor: COLORS.ebony}}>
      <Sequence durationInFrames={30}>
        <BoothExteriorBeat />
      </Sequence>
      <Sequence from={21} durationInFrames={78}>
        <BoothExperienceBeat />
      </Sequence>
    </AbsoluteFill>
  );
};
