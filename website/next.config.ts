import type { NextConfig } from "next";

import { siteBasePath } from "./site.config";

const nextConfig: NextConfig = {
  basePath: siteBasePath,
  devIndicators: false,
  output: "standalone",
};

export default nextConfig;
