# Impeccable Design Context - Massar Academy / مسار أكاديمي

> "تعلم. نتطور. نحقق."

Source of truth: `/Users/mazenelsbagh/Downloads/brand identyty.pdf`, a 7-page visual brand identity guide with no selectable text layer. This document captures the actionable product and design direction from that guide.

---

## Product Register

**Register:** product

Massar Academy is an Arabic-first learning platform. The design serves student progress, parent/assistant follow-up, and admin control. It should feel like a disciplined educational system with a strong visual identity, not a generic course marketplace.

---

## Brand Identity

### Brand Names

- Arabic: **مسار أكاديمي**
- English: **Massar Academy**
- Short name in product copy: **مسار**

### Core Idea

The logo and visual language express an upward learning journey: steps, progress, graduation, and a student moving toward a clear goal. The student does not wander through content; they climb a structured path.

### Brand Promise

**رحلة تعليمية تبدأ بخطوة**
Every product surface should make the next step clear, achievable, and connected to measurable progress.

### Brand Personality

- **ملهم:** يحفز الطموح.
- **متطور:** يواكب المستقبل.
- **موثوق:** يبني الثقة.

### Tone

- Arabic-first, clear, confident, and encouraging.
- Modern and professional, not childish.
- Motivational without exaggeration.
- Direct CTAs: "ابدأ الآن", "ابدأ رحلتك التعليمية", "خطوتك التالية".

### Approved Taglines And Copy Patterns

- تعلم. نتطور. نحقق.
- هوية تلهم، ومسار يصنع الفرق.
- ابدأ رحلتك التعليمية.
- خطواتك الأولى نحو التفوق.
- كل خطوة تقربك من هدفك.
- ابدأ الآن، مستقبلك ينتظرك.
- تعلم اليوم لتصنع غدك.
- تعلم بذكاء وحقق أهدافك.

---

## Users

### Primary Users

Egyptian and Arabic-speaking secondary students using mobile phones to study, watch lessons, solve exams, and track progress.

### Secondary Users

Parents, teaching assistants, teachers, and admins who need reliable oversight, reporting, moderation, content management, and operational control.

### Context Of Use

- Students mostly enter to finish a specific task: watch, solve, review, or unlock the next step.
- Student surfaces are mobile-first and should support one-handed use.
- Admin and assistant surfaces are desktop-first, dense, and workflow-oriented.
- Every path should reduce uncertainty: where am I, what is complete, what is next, what is blocked.

### Target Emotional State

- **Direction:** I know my path.
- **Progress:** I can see I am moving up.
- **Confidence:** The system is organized and trustworthy.
- **Momentum:** The next step feels within reach.
- **Achievement:** Completion feels meaningful.

---

## Aesthetic Direction

### Visual Tone

Modern academic, optimistic, clean, and structured. The identity is built around a deep navy foundation, teal progress accents, warm gold achievement cues, and off-white digital surfaces.

### Visual Metaphors

- Steps and stair paths.
- Growth bars and upward arrows.
- Dotted progress grids.
- Thin route lines.
- Graduation cap and achievement markers.
- Student figure moving upward.
- Circular arcs for continuity and learning loops.

### Photography Style

- Optimistic, clean, youthful, and inspiring.
- Students should look ambitious and focused.
- Educational environments should feel bright, organized, and modern.
- Avoid stock-photo clutter, heavy filters, dark dramatic lighting, and unrelated lifestyle imagery.

### What To Avoid

- Old gold/cream luxury identity from previous docs.
- Pharaonic or heritage decoration unless explicitly present in assets.
- Neon colors, purple-blue gradients, cartoonish education graphics.
- Generic SaaS card grids and decorative glassmorphism.
- Overcrowded tutoring-center visuals.
- Random icons not tied to learning, progress, trust, time, ideas, or goals.

---

## Design Principles

### 1. Every Screen Shows The Path

The student should always understand the current step, the next step, and the end goal. Use stair/progress metaphors where they clarify, not as decoration.

### 2. Progress Is Visual

Completion, growth, ranking, watch progress, exam progress, and unlocked content should be visible and motivating. Teal carries motion and progress. Gold marks achievement.

### 3. Trust Before Flash

Deep navy is the authority color. It should anchor navigation, headings, key controls, and important states. The interface should feel stable and credible.

### 4. Mobile First For Students

Student workflows must have clear touch targets, no horizontal traps, readable Arabic typography, and concise actions.

### 5. Structured, Not Sterile

The interface should be clean and systematic, but not empty. Use branded steps, dots, arcs, and icons to create identity while preserving task focus.

---

## Technical Design Constraints

- Stack: Next.js 16, React 19, TypeScript strict, Tailwind CSS 4, Framer Motion, Zustand, existing shared components.
- Direction: RTL by default, with English support where needed.
- Arabic font: Tajawal.
- English font: Montserrat.
- Accessibility: WCAG AA, minimum 4.5:1 contrast for body text.
- Touch targets: at least 44px on student/mobile surfaces.
- Motion: support `prefers-reduced-motion`; no motion-dependent interactions.
- Performance: student-facing pages should minimize JavaScript and avoid decorative heavy animation unless it directly supports progress or comprehension.

---

## Brand Tokens

### Core Palette

```text
Deep Navy:    #0A1D3D
Teal:         #0E8F8F
Warm Gold:    #D4A017
Off White:    #F6F7F8
Dark Gray:    #2E3A47
Light Gray:   #DCE1E6
Soft Gray:    #EEF1F4
White:        #FFFFFF
```

### Token Intent

- **Deep Navy:** primary brand, authority, headings, navigation, major surfaces.
- **Teal:** progress, active states, highlights, route lines, icons.
- **Warm Gold:** achievement, graduation, milestones, premium emphasis, small separators.
- **Off White:** default app canvas and marketing/application backgrounds.
- **Dark Gray:** secondary text and supporting copy.
- **Light Gray / Soft Gray:** dividers, subtle surfaces, inactive areas.

### Typography

- Arabic headings: Tajawal Bold.
- Arabic body: Tajawal Regular.
- English headings: Montserrat Bold.
- English body: Montserrat Regular.

### Copy Scale From Brand Guide

- Heading: Tajawal Bold, 36-40px.
- Subheading: Tajawal Medium, 20-24px.
- Body: Tajawal Regular, 14-16px.
- Caption: Tajawal Regular, 11-12px.

---

## Implementation Rules

- Use navy as the main authority color, not gold.
- Use teal for progress and active feedback.
- Use gold sparingly for achievement and brand separators.
- Keep off-white and soft gray surfaces clean and bright.
- Use steps, dots, arcs, and route lines as branded structure when they clarify progress.
- Never distort, rotate, recolor, shadow, or add effects to the logo.
- Minimum digital logo width: 20mm equivalent.
- Preserve clear space around the logo equal to the simplified icon spacing shown in the guide.
- Prefer clean line icons in teal/navy with consistent stroke.
- Avoid pure black for UI text unless forced by media overlays; use Deep Navy or Dark Gray.
