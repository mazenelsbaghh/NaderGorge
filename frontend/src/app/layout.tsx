import type { Metadata } from "next";
import { Cairo } from "next/font/google";
import "./globals.css";
import { AuthBootstrap } from "@/components/layout/AuthBootstrap";
import { GlobalNav } from "@/components/layout/GlobalNav";

const cairo = Cairo({
  variable: "--font-cairo",
  subsets: ["arabic", "latin"],
  weight: ["300", "400", "500", "600", "700", "800", "900"],
});

export const metadata: Metadata = {
  title: "أكاديمية نادر جورج",
  description: "المنصة المتكاملة للتعليم الثانوي",
};

import { Toaster } from "react-hot-toast";

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="ar"
      dir="rtl"
      className={`${cairo.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <body
        className="min-h-full flex flex-col font-[family-name:var(--font-cairo)]"
        suppressHydrationWarning
      >
        <AuthBootstrap />
        <GlobalNav />
        {children}
        <Toaster position="bottom-left" />
      </body>
    </html>
  );
}
