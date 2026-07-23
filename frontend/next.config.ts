import type { NextConfig } from "next";
import withBundleAnalyzer from "@next/bundle-analyzer";

const nextConfig: NextConfig = {
  reactCompiler: true,
  allowedDevOrigins: [
    '192.168.1.203',
    'app.lvh.me',
    'admin.lvh.me',
    'staff.lvh.me',
    'teacher.lvh.me',
  ],
  devIndicators: false,
  // Enables standalone output for minimal Docker images.
  // Only the production-necessary files are copied into the final image layer.
  output: 'standalone',
  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: 'assets.massar-academy.net',
      },
    ],
  },
};

const withBundleAnalyzerConfig = withBundleAnalyzer({
  enabled: process.env.ANALYZE === 'true',
});

export default withBundleAnalyzerConfig(nextConfig);
