# Implementation Plan: Landing Page Reference Redesign

**Branch**: `080-landing-page-reference-redesign` | **Date**: 2026-06-06 | **Spec**: `specs/080-landing-page-reference-redesign/spec.md`

## Summary

Rebuild the public landing page to match the user's visual reference and new Massar Academy identity. The implementation updates only frontend landing assets, data, components, and landing-specific CSS tokens. Backend behavior is unchanged.

## Technical Context

**Language/Version**: TypeScript 5.x, Next.js 16.2.7, React 19  
**Primary Dependencies**: Next.js App Router, Tailwind CSS 4, framer-motion, lucide-react  
**Storage**: N/A  
**Testing**: `npm run lint`, local browser verification  
**Target Platform**: Public web, mobile-first with desktop reference fidelity  
**Constraints**: Arabic RTL, WCAG AA contrast, no gold as primary CTA, no oversized decorative card rounding, no text overflow.

## Design Direction

- Physical scene: an Egyptian student opens the homepage on a phone before studying; the surface must feel clear, focused, and premium, with enough energy to start now.
- Palette strategy: restrained white and soft gray base, committed deep navy typography, teal action color, gold only as sparse achievement detail.
- Hero structure: copy left, image right on desktop, stacked on mobile.
- Component structure: keep landing data in `data.ts`, page sections in `components/landing`, and shared visual rules in `globals.css`.

## Files To Modify

- `frontend/public/images/landing-hero.png`
- `frontend/src/app/globals.css`
- `frontend/src/components/landing/data.ts`
- `frontend/src/components/landing/LandingNav.tsx`
- `frontend/src/components/landing/HeroSection.tsx`
- `frontend/src/components/landing/CircularGallerySection.tsx`
- `frontend/src/components/landing/TestimonialsSection.tsx`
- `frontend/src/components/landing/LandingFooter.tsx`
- `frontend/src/app/page.tsx`

## Constitution Check

- Modular Clean Architecture: pass, landing-only frontend changes.
- Provider Abstraction First: not applicable, no provider change.
- Premium Editorial Design System: pass, uses reference-driven visual hierarchy and existing brand tokens.
- Mobile First: pass, all sections use responsive stacking and stable dimensions.
