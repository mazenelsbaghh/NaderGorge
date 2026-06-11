import type { NextConfig } from "next";
import withBundleAnalyzer from "@next/bundle-analyzer";

const nextConfig: NextConfig = {
  reactCompiler: true,
  allowedDevOrigins: ['192.168.1.203'],
  // Enables standalone output for minimal Docker images.
  // Only the production-necessary files are copied into the final image layer.
  output: 'standalone',
};

const withBundleAnalyzerConfig = withBundleAnalyzer({
  enabled: process.env.ANALYZE === 'true',
});

export default withBundleAnalyzerConfig(nextConfig);
