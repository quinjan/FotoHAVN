import {PHOTO} from "../../../shared/theme";
import {
  WeekendClubEditorialFrame,
  WeekendClubEditorialShell,
} from "../components/WeekendClubEditorialFrame";

export const PackedHookScene: React.FC = () => {
  return (
    <WeekendClubEditorialShell>
      <WeekendClubEditorialFrame
        photo={PHOTO.guest874}
        durationInFrames={51}
        fadeIn={false}
        fadeOut={false}
        startScale={1.002}
        endScale={1.014}
      />
    </WeekendClubEditorialShell>
  );
};
