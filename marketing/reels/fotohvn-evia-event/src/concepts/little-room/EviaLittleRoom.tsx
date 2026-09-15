import {TransitionSeries, linearTiming} from "@remotion/transitions";
import {fade} from "@remotion/transitions/fade";
import {AbsoluteFill} from "remotion";
import {GuideAudio} from "../../shared/GuideAudio";
import {LittleRoomBoothScene} from "./scenes/LittleRoomBoothScene";
import {LittleRoomFinalScene} from "./scenes/LittleRoomFinalScene";
import {MallFullScene} from "./scenes/MallFullScene";
import {MiddleOfItScene} from "./scenes/MiddleOfItScene";
import {PeopleKeepsakesScene} from "./scenes/PeopleKeepsakesScene";

export const EviaLittleRoom: React.FC = () => {
  return (
    <AbsoluteFill>
      <TransitionSeries>
        <TransitionSeries.Sequence durationInFrames={81} name="Weekend Finds Club">
          <MallFullScene />
        </TransitionSeries.Sequence>
        <TransitionSeries.Transition
          presentation={fade()}
          timing={linearTiming({durationInFrames: 9})}
        />
        <TransitionSeries.Sequence durationInFrames={117} name="Find FOTOHVN">
          <MiddleOfItScene />
        </TransitionSeries.Sequence>
        <TransitionSeries.Transition
          presentation={fade()}
          timing={linearTiming({durationInFrames: 9})}
        />
        <TransitionSeries.Sequence durationInFrames={99} name="A little room">
          <LittleRoomBoothScene />
        </TransitionSeries.Sequence>
        <TransitionSeries.Transition
          presentation={fade()}
          timing={linearTiming({durationInFrames: 9})}
        />
        <TransitionSeries.Sequence durationInFrames={189} name="People and keepsakes">
          <PeopleKeepsakesScene />
        </TransitionSeries.Sequence>
        <TransitionSeries.Transition
          presentation={fade()}
          timing={linearTiming({durationInFrames: 9})}
        />
        <TransitionSeries.Sequence durationInFrames={90} name="Visit today CTA">
          <LittleRoomFinalScene />
        </TransitionSeries.Sequence>
      </TransitionSeries>
      <GuideAudio />
    </AbsoluteFill>
  );
};
