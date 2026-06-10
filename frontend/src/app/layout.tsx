import "@/lib/timezone-bootstrap";
import type { Metadata } from "next";
import { Tajawal, Montserrat } from "next/font/google";
import "./globals.css";
import { AuthBootstrap } from "@/components/layout/AuthBootstrap";
import { GlobalNav } from "@/components/layout/GlobalNav";
import { Toaster } from "react-hot-toast";

const tajawal = Tajawal({
  variable: "--font-tajawal",
  subsets: ["arabic"],
  weight: ["400", "500", "700", "800"],
});

const montserrat = Montserrat({
  variable: "--font-montserrat",
  subsets: ["latin"],
  weight: ["500", "700"],
});

export const metadata: Metadata = {
  title: "منصة مسار | Massar Platform",
  description: "المنصة المتكاملة للتعليم الثانوي - منصة مسار",
};

const surfaceInitScript = `
  (function () {
    try {
      var surface = '${process.env.NEXT_PUBLIC_APP_SURFACE || ""}';
      if (!surface || surface === 'all') {
        surface = 'landing';
        var host = window.location.host;
        if (host) {
          var port = host.split(':')[1];
          if (port === '8738') surface = 'landing';
          else if (port === '8739') surface = 'student';
          else if (port === '8740') surface = 'admin';
          else if (port === '8741') surface = 'teacher';
          else if (port === '8742') surface = 'assistant';
          else {
            var domainOnly = host.split(':')[0];
            if (domainOnly.startsWith('admin.') || domainOnly.startsWith('super.')) surface = 'admin';
            else if (domainOnly.startsWith('app.') || domainOnly.startsWith('student.')) surface = 'student';
            else if (domainOnly.startsWith('teacher.')) surface = 'teacher';
            else if (domainOnly.startsWith('staff.') || domainOnly.startsWith('assistant.')) surface = 'assistant';
          }
        }
      }
      document.documentElement.setAttribute('data-massar-surface', surface);
    } catch (_) {}
  })();
`;

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
  const surface = process.env.NEXT_PUBLIC_APP_SURFACE || 'landing';

  return (
    <html
      lang="ar"
      dir="rtl"
      data-massar-surface={surface}
      className={`${tajawal.variable} ${montserrat.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: surfaceInitScript }} />
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

