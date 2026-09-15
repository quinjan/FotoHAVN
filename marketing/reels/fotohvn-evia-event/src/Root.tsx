import "./index.css";
import {Composition, Folder} from "remotion";
import {EviaLittleRoom} from "./concepts/little-room/EviaLittleRoom";
import {EviaPackedToday} from "./concepts/packed/EviaPackedToday";

export const RemotionRoot: React.FC = () => {
  return (
    <Folder name="Evia-Mall-Reels">
      <Composition
        id="Evia-Packed-Today"
        component={EviaPackedToday}
        durationInFrames={450}
        fps={30}
        width={1080}
        height={1920}
      />
      <Composition
        id="Evia-Little-Room"
        component={EviaLittleRoom}
        durationInFrames={540}
        fps={30}
        width={1080}
        height={1920}
      />
    </Folder>
  );
};
