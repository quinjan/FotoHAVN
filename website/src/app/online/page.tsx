import type { Metadata } from "next";
import OnlinePhotobooth from "@/components/onlinePhotobooth/OnlinePhotobooth";

export const metadata: Metadata = {
  title: "The Online Booth | FOTOHAVN",
  description: "Choose a frame, find your light, and make a digital keepsake in the FOTOHAVN online photobooth.",
};

export default function OnlineBoothPage() {
  return <OnlinePhotobooth />;
}
