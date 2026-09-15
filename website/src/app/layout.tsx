import type { Metadata, Viewport } from "next";
import { Cormorant_Garamond, Manrope } from "next/font/google";
import { siteUrl, withSiteBasePath } from "../../site.config";
import "./globals.css";

const displayFont = Cormorant_Garamond({
  variable: "--font-cormorant",
  subsets: ["latin"],
  weight: ["400", "500", "600"],
  style: ["normal", "italic"],
  display: "swap",
});

const sansFont = Manrope({
  variable: "--font-manrope",
  subsets: ["latin"],
  display: "swap",
});

export const metadata: Metadata = {
  title: "FOTOHAVN | Photographs, Developed Differently",
  description:
    "An enclosed vintage photobooth experience for celebrations worth remembering.",
  metadataBase: new URL(siteUrl),
  alternates: {
    canonical: "/",
  },
  robots: {
    follow: true,
    index: true,
  },
  openGraph: {
    title: "FOTOHAVN | Photographs, Developed Differently",
    description:
      "An enclosed vintage photobooth experience for celebrations worth remembering.",
    images: [
      new URL(
        withSiteBasePath("/images/experience-online/keepsake-reunion.webp"),
        new URL(siteUrl).origin,
      ),
    ],
    url: "/",
    type: "website",
  },
};

export const viewport: Viewport = {
  themeColor: "#FBF8F2",
  colorScheme: "light",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${displayFont.variable} ${sansFont.variable}`}>
      <body>{children}</body>
    </html>
  );
}
