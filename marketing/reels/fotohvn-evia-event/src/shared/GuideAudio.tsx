import {Audio} from "@remotion/media";
import {staticFile} from "remotion";

type GuideAudioProps = {
  temporaryClearedTrack?: string;
};

export const GuideAudio: React.FC<GuideAudioProps> = ({
  temporaryClearedTrack,
}) => {
  if (!temporaryClearedTrack) {
    return null;
  }

  return (
    <Audio
      src={staticFile(temporaryClearedTrack)}
      volume={0.12}
      loop
      name="TEMPORARY CLEARED GUIDE TRACK — REPLACE BEFORE EXPORT"
    />
  );
};
