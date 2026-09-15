import {Sequence} from "remotion";
import {PHOTO} from "../../../shared/theme";
import {
  WeekendClubEditorialFrame,
  WeekendClubEditorialShell,
} from "../components/WeekendClubEditorialFrame";

export const GuestBurstScene: React.FC = () => {
  return (
    <WeekendClubEditorialShell>
      <Sequence durationInFrames={28}>
        <WeekendClubEditorialFrame
          photo={PHOTO.guest874}
          durationInFrames={28}
          fadeIn={false}
        />
      </Sequence>
      <Sequence from={20} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guest875} durationInFrames={28} />
      </Sequence>
      <Sequence from={40} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guest876} durationInFrames={28} />
      </Sequence>
      <Sequence from={60} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guest877} durationInFrames={28} />
      </Sequence>
      <Sequence from={80} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guest878} durationInFrames={28} />
      </Sequence>
      <Sequence from={100} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guest879} durationInFrames={28} />
      </Sequence>
      <Sequence
        from={120}
        durationInFrames={28}
        style={{
          scale: 1.292,
        }}
      >
        <WeekendClubEditorialFrame photo={PHOTO.guest880} durationInFrames={28} />
      </Sequence>
      <Sequence from={140} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guest881} durationInFrames={28} />
      </Sequence>
      <Sequence from={160} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guest882} durationInFrames={28} />
      </Sequence>
      <Sequence from={180} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestCouple} durationInFrames={28} />
      </Sequence>
      <Sequence from={200} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestGroupFour} durationInFrames={28} />
      </Sequence>
      <Sequence from={220} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded01} durationInFrames={28} />
      </Sequence>
      <Sequence from={240} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded03} durationInFrames={28} />
      </Sequence>
      <Sequence from={260} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded04} durationInFrames={28} />
      </Sequence>
      <Sequence from={280} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded05} durationInFrames={28} />
      </Sequence>
      <Sequence from={300} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded06} durationInFrames={28} />
      </Sequence>
      <Sequence from={320} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded07} durationInFrames={28} />
      </Sequence>
      <Sequence from={340} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded08} durationInFrames={28} />
      </Sequence>
      <Sequence from={360} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded09} durationInFrames={28} />
      </Sequence>
      <Sequence from={380} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded10} durationInFrames={28} />
      </Sequence>
      <Sequence from={400} durationInFrames={28}>
        <WeekendClubEditorialFrame photo={PHOTO.guestAdded11} durationInFrames={28} />
      </Sequence>
      <Sequence from={420} durationInFrames={30}>
        <WeekendClubEditorialFrame
          photo={PHOTO.guestAdded12}
          durationInFrames={30}
          fadeOut={false}
        />
      </Sequence>
    </WeekendClubEditorialShell>
  );
};
