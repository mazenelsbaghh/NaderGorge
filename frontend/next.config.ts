import type { NextConfig } from "next";
import withBundleAnalyzer from "@next/bundle-analyzer";
import { fileURLToPath } from "node:url";

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
  // Production containers are intentionally read-only. Keep ISR/fetch cache
  // entries in bounded process memory instead of writing into .next/server.
  // The image optimizer still uses the dedicated .next/cache tmpfs mount.
  cacheHandler: fileURLToPath(new URL('./cache-handler.cjs', import.meta.url)),
  cacheMaxMemorySize: 50 * 1024 * 1024,
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
