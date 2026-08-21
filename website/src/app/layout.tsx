import type { Metadata, Viewport } from "next";
import { Cormorant_Garamond, Manrope } from "next/font/google";
import "./globals.css";

const displayFont = Cormorant_Garamond({
  variable: "--font-cormorant",
  subsets: ["latin"],
  weight: ["400", "500", "600"],
  display: "swap",
});

const sansFont = Manrope({
  variable: "--font-manrope",
  subsets: ["latin"],
  display: "swap",
});

export const metadata: Metadata = {
  title: "FOTOHVN | Photographs, Developed Differently",
  description:
    "An enclosed vintage photobooth experience in the Philippines for celebrations worth remembering.",
  metadataBase: new URL("https://fotohvn.com"),
  openGraph: {
    title: "FOTOHVN | Photographs, Developed Differently",
    description:
      "An enclosed vintage photobooth experience for celebrations worth remembering.",
    images: ["/images/hero-booth.png"],
    type: "website",
  },
};

export const viewport: Viewport = {
  themeColor: "#FBF8F2",
  colorScheme: "light",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${displayFont.variable} ${sansFont.variable}`}
    >
      <body>{children}</body>
    </html>
  );
}
