import {AbsoluteFill, Sequence} from "remotion";
import {PortraitPrint} from "../../../shared/PortraitPrint";
import {COLORS, PHOTO} from "../../../shared/theme";

export const PeopleKeepsakesScene: React.FC = () => {
  return (
    <AbsoluteFill style={{backgroundColor: COLORS.darkWalnut}}>
      <Sequence durationInFrames={36}>
        <PortraitPrint
          src={PHOTO.guest875}
          label="REAL PEOPLE."
          rotation={-0.8}
          fadeFrames={6}
          shotDurationInFrames={36}
        />
      </Sequence>
      <Sequence from={36} durationInFrames={36}>
        <PortraitPrint
          src={PHOTO.guest876}
          rotation={0.7}
          fadeFrames={6}
          shotDurationInFrames={36}
        />
      </Sequence>
      <Sequence from={72} durationInFrames={36}>
        <PortraitPrint
          src={PHOTO.guest877}
          label="REAL LAUGHTER."
          rotation={-0.6}
          fadeFrames={6}
          shotDurationInFrames={36}
        />
      </Sequence>
      <Sequence from={108} durationInFrames={36}>
        <PortraitPrint
          src={PHOTO.guest879}
          rotation={0.9}
          fadeFrames={6}
          shotDurationInFrames={36}
        />
      </Sequence>
      <Sequence from={144} durationInFrames={45}>
        <PortraitPrint
          src={PHOTO.guest882}
          label="PHOTOGRAPHS TO KEEP."
          rotation={-0.7}
          fadeFrames={8}
          shotDurationInFrames={45}
        />
      </Sequence>
    </AbsoluteFill>
  );
};
