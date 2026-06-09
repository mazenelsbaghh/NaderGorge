# UI/UX Master Plan

**Last Updated**: 2026-06-09

---

## Active Plans

### Assistant Surface Layout & Design (2026-06-09)
- [x] Implement AssistantShellChrome with Arabic-first RTL support, Sand/Gold tones, ambient shadows, and Cairo/Tajawal fonts.
- [x] Create premium task board cards with dynamic priority badges (High, Critical, Medium, Low) and task status indicators.
- [x] Integrate responsive drawer menu for mobile assistant surfaces.


### Landing Page Dark Mode & Auto-Swiper Refinements (2026-06-06)
- [x] Refine `CircularGallerySection.tsx` to make the student stack auto-move on its own (simulating mouse drags).
- [x] Map landing theme custom variables in `@theme inline` in `globals.css` and use them in landing components instead of hardcoded colors.
- [x] Support dark mode backgrounds and overlays for `.landing-page`, `.landing-section`, and `.student-app-background` in `globals.css`.
- [x] Connect `useAdminTheme` in `HeroSection.tsx` and `LandingFooter.tsx` to switch background/content hero images and gradient overlays to dark mode versions.
- [x] Invert `logo-mark.svg` in dark mode inside the navigation bar.

### Landing Page Overdrive: 3D Swiper Stack & Kinetic Reveals (2026-06-06)
- [x] Update `HeroSection.tsx` with kinetic typography and cursor-following spotlights.
- [x] Re-architect `CircularGallerySection.tsx` to display student ranks in a draggable 3D Card Stack Swiper.
- [x] Re-architect `CircularGallerySection.tsx` teachers list into an infinite auto-scrolling horizontal marquee.
- [x] Add auto-swipe cycle timer with drag-to-dismiss exit animation to the student 3D Card Stack Swiper.

### Landing Page Overdrive: Cinematic Reveals & Canvas Particles (2026-06-06)
- [x] Create interactive canvas component `ScholarlyParticles.tsx` for brand-colored ambient particle animation.
- [x] Update `HeroSection.tsx` with scroll-driven parallax and reveal animations.
- [x] Update `CircularGallerySection.tsx` with staggered viewport entry animations.
- [x] Update `TestimonialsSection.tsx` with viewport reveals.
- [x] Update `home.tsx` to wrap components and introduce the particles fixed background.

### Brand Identity Change to Massar Academy / مسار أكاديمي (2026-06-05)
- [/] Analyze brand identity PDF and logo SVG to extract design system parameters.
- [ ] Run `impeccable teach` to set up new brand context (`PRODUCT.md` and `DESIGN.md`).
- [ ] Implement color strategy changes (Deep Navy `#0A1D3D`, Teal `#0E8F8F`, Warm Gold `#D4A017`).
- [ ] Apply typography hierarchy using Tajawal (Arabic) and Montserrat (English).
- [ ] Replace name "Nader Gorge" with "Massar Academy" / "مسار أكاديمي" and update logos across frontend.

### Student Forgot Password Interface (2026-06-04)
- [x] Adhere to the "No-Line Rule" using tonal background offsets instead of borders where possible.
- [x] Incorporate Cairo typography and HSL-based theme tokens.
- [x] Implement responsive elements (desktop-centered, mobile bottom nav integration).
- [x] Integrate dot-grid background and ambient glow.
- [x] Deliver a native Arabic RTL wizard layout with custom transitions.

---

## History
- **2026-06-06**: Upgraded Landing Page Overdrive with a draggable auto-cycling 3D Card Stack Swiper for student ranks, kinetic typography reveals in the Hero, and an infinite auto-scrolling row for teachers.
- **2026-06-06**: Completed Landing Page Overdrive with custom interactive canvas particles and Framer Motion viewport reveal animations.
- **2026-06-05**: Analyzed the new Brand Identity PDF and Logo SVG, and drafted the migration plan to Massar Academy.
- **2026-06-04**: Initialized UI/UX master plan directory and completed the Student Forgot Password interface work.
