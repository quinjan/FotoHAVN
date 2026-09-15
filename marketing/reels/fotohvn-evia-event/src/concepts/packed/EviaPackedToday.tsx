import {TransitionSeries} from "@remotion/transitions";
import {AbsoluteFill} from "remotion";
import {GuideAudio} from "../../shared/GuideAudio";
import {GuestBurstScene} from "./scenes/GuestBurstScene";

export const EviaPackedToday: React.FC = () => {
  return (
    <AbsoluteFill>
      <TransitionSeries>
        <TransitionSeries.Sequence
          durationInFrames={450}
          name="Weekend Club guest slideshow"
        >
          <GuestBurstScene />
        </TransitionSeries.Sequence>
      </TransitionSeries>
      <GuideAudio />
    </AbsoluteFill>
  );
};
