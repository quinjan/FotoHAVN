import type { NextConfig } from "next";

import { siteBasePath } from "./site.config";
import eventPhotoSizes from "./event-photo-sizes.json";

const nextConfig: NextConfig = {
  basePath: siteBasePath,
  devIndicators: false,
  output: "standalone",
  images: eventPhotoSizes,
};

export default nextConfig;
