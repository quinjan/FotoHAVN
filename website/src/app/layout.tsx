import type { Metadata, Viewport } from "next";
import { Cormorant_Garamond, Manrope } from "next/font/google";
import {
  stagingSiteUrl,
  withSiteBasePath,
} from "../../site.config";
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
    "An enclosed vintage photobooth experience for celebrations worth remembering.",
  metadataBase: new URL(stagingSiteUrl),
  robots: {
    follow: false,
    index: false,
  },
  openGraph: {
    title: "FOTOHVN | Photographs, Developed Differently",
    description:
      "An enclosed vintage photobooth experience for celebrations worth remembering.",
    images: [
      new URL(
        withSiteBasePath("/images/hero-booth.png"),
        new URL(stagingSiteUrl).origin,
      ),
    ],
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
      <head>
        <link rel="preconnect" href="https://api.fontshare.com" />
        <link rel="preconnect" href="https://cdn.fontshare.com" crossOrigin="anonymous" />
        <link
          rel="stylesheet"
          href="https://api.fontshare.com/v2/css?f[]=cabinet-grotesk@400,500,700&display=swap"
        />
      </head>
      <body>{children}</body>
    </html>
  );
}
