import type { Metadata } from "next";
import { Tajawal, Montserrat } from "next/font/google";
import "./globals.css";
import { AuthBootstrap } from "@/components/layout/AuthBootstrap";
import { GlobalNav } from "@/components/layout/GlobalNav";

const tajawal = Tajawal({
  variable: "--font-tajawal",
  subsets: ["arabic"],
  weight: ["300", "400", "500", "700", "800", "900"],
});

const montserrat = Montserrat({
  variable: "--font-montserrat",
  subsets: ["latin"],
  weight: ["300", "400", "500", "600", "700", "800", "900"],
});

export const metadata: Metadata = {
  title: "منصة مسار | Masar Platform",
  description: "المنصة المتكاملة للتعليم الثانوي - منصة مسار",
};

import { Toaster } from "react-hot-toast";

const themeInitScript = `
  (function () {
    try {
      var storageKey = 'admin-theme-mode';
      var storedMode = window.localStorage.getItem(storageKey);
      var mode = storedMode === 'dark' ? 'dark' : 'light';
      var root = document.documentElement;

      root.dataset.themeMode = mode;
      root.classList.toggle('dark', mode === 'dark');
      root.style.backgroundColor = 'var(--admin-bg)';
      root.style.backgroundImage = 'radial-gradient(circle at 12% 12%, rgba(14, 143, 143, 0.08), transparent 28%), radial-gradient(circle at 78% 20%, rgba(10, 29, 61, 0.06), transparent 30%), linear-gradient(180deg, #ffffff 0%, #f8fafc 100%)';
      root.style.backgroundSize = 'auto';
      root.style.backgroundRepeat = 'repeat';
    } catch (_) {}
  })();
`;

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="ar"
      dir="rtl"
      className={`${tajawal.variable} ${montserrat.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body
        className="min-h-full flex flex-col font-[family-name:var(--font-tajawal)]"
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
