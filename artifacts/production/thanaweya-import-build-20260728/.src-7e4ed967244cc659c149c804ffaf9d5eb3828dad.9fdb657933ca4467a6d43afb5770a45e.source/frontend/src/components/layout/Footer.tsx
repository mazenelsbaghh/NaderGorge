export function Footer() {
  return (
    <footer className="border-t border-[var(--landing-line)] bg-[var(--landing-bg)]">
      <div className="container mx-auto px-4 py-6 text-center text-sm text-[var(--landing-muted)]">
        <p>&copy; {new Date().getFullYear()} منصة مسار التعليمية. جميع الحقوق محفوظة.</p>
      </div>
    </footer>
  );
}
