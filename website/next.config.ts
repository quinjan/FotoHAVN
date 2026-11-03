import type { NextConfig } from "next";

import { siteBasePath } from "./site.config";
import eventPhotoSizes from "./event-photo-sizes.json";

const nextConfig: NextConfig = {
  basePath: siteBasePath,
  devIndicators: false,
  output: "standalone",
  images: eventPhotoSizes,
  async redirects() {
    return [
      { source: "/fotohavn", destination: "/", permanent: true },
      { source: "/fotohavn/:path*", destination: "/:path*", permanent: true },
    ];
  },
};

export default nextConfig;
