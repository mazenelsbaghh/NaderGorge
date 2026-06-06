# Impeccable Design Context — Masar Platform / منصة مسار

> "We are not building a learning platform, we are building a controlled learning system that drives results."

---

## Design Context

### Users

**Primary Users:** Egyptian secondary and baccalaureate students (First/Second Secondary, First/Second Baccalaureate) — mostly teenagers using mobile phones, studying history through the Masar Platform curriculum.

**Secondary Users:** Parents (viewing reports), Teaching Assistants (reviewing homework, following up), the Teacher himself (academic oversight, analytics), and Admins (full system control).

**Context of Use:**
- Students open the platform to **study and leave** — not to browse or socialize.
- Sessions are goal-oriented: watch a lesson, solve homework, check progress, move on.
- Most students access from **mobile**, often with one hand, in short focused bursts.
- Assistants and the teacher primarily use **desktop** for management workflows.

**The Job to Be Done:**
- Understand history more easily through structured, story-driven lessons.
- Memorize faster with controlled repetition and progress tracking.
- Solve more exam questions through practice and AI-generated tasks.
- Stay committed through positive pressure and clear study paths.
- Improve grades through follow-up, gamification, and accountability.

**Target Emotional State:**
- **Control** — "I know where I am and where I'm going."
- **Progress** — "I'm actually moving forward, not wasting time."
- **Positive Pressure** — "I need to finish what's on my plate."
- **Confidence** — "I'm going to get a good grade."
- **Clarity** — "The plan is crystal clear."

**What to Avoid:**
- Distraction, scrolling-for-no-reason energy, social media vibes.
- The student should never feel lost, overwhelmed, or aimless.

> **Guiding user mantra:** "أنا داخل أخلص اللي عليا وأمشي" — "I'm here to finish what I need to do and leave."

---

### Brand Personality

**Three Words:** شبابي • مسيطر • ملهم (Youthful • Commanding • Inspiring)

**Voice & Tone:**
- Direct, confident, no fluff.
- Encouraging but not soft — like a coach, not a friend.
- Clear instructions, not vague suggestions.
- Arabic-first, formal-casual balance (not too stiff, not too casual).

**Emotional Goals:**
- The platform should feel like a **high-end product**, not a tutoring center website.
- It should inspire the same trust and seriousness as a premium tool.
- "دي مش منصة دروس… دي نظام بيدير مذاكرتي" — "This isn't a lessons platform... this is a system managing my studying."

---

### Aesthetic Direction

**Visual Tone:** Modern Egyptian — a subtle, confident pharaonic identity without kitsch or heavy ornamentation. Think luxury editorial meets structured learning system.

**References (what to feel like):**
| Reference | What We Take |
|-----------|-------------|
| **Notion** | Organization, calm, simplicity, clean whitespace |
| **Duolingo** | Motivation mechanics, visible progress, dopamine of completion |
| **Apple** | Clean hierarchy, confidence, no clutter, premium feel |

**Anti-References (what to NOT feel like):**
- Overcrowded tutoring center websites with clashing colors.
- PowerPoint-style layouts with text-heavy screens.
- Childish/cartoonish educational platforms.
- Neon colors, excessive gradients, or visual noise.
- Anything that screams "سنتر دروس" (tutoring center).

> **Our bar:** This should feel like a **product** — not a website someone made in a weekend.

**Theme Strategy:**
- **Light Mode** = Gold Identity — warm, editorial, the brand's signature canvas. Sand (`#faf2e6`), burnished gold (`#9a6933`), deep umber text (`#2c1708`).
- **Dark Mode** = Premium Calm — a separate, restful experience. Not gold-heavy. Quiet, professional, refined. Comfortable for night studying.

**Typography:**
- **Cairo** font family (Arabic + Latin) — geometric, modern, excellent Arabic support.
- Strong weight contrast between headings and body.
- Generous letter-spacing on labels and micro-copy.
- Readable sizes — never sacrifice legibility for aesthetics.

**Color Palette:**
- **Use:** Brand gold as primary, neutral tones (whites, warm grays, soft blacks).
- **Avoid:** Loud/saturated colors, neon, excessive red. Destructive red used sparingly and only for real errors.
- All shadows tinted warm (umber-based) — never pure black shadows.

**Iconography:**
- Clean, minimal line icons.
- Subtle pharaonic touches where appropriate (not on every icon).
- Consistent stroke weight and sizing across the system.

**Spacing & Layout:**
- Generous whitespace — editorial breathing room.
- Asymmetric margins for editorial feel.
- No 1px solid borders — use tonal background shifts for separation.
- Sections separated by spacing and surface-color hierarchy, not dividers.

**Amplify:**
- Strong typography hierarchy.
- Respectful spacing (never cramped).
- Clean iconography with cultural accents.
- Glassmorphism on floating elements.
- Subtle micro-animations on progress and completion.

**Reduce:**
- Heavy ornamentation / decorative patterns.
- Unnecessary visual elements.
- Details without functional purpose.
- Dense information displays.

---

### Design Principles

**1. Clarity Over Decoration**
Every element must earn its place. If it doesn't help the student understand where they are or what to do next, remove it. Decoration without purpose is noise.

**2. Progress is Visible**
The student should always see tangible evidence of their advancement — completion percentages, streaks, unlocked content, ranking changes. Progress is the product's core dopamine loop.

**3. Mobile-First, Always**
Design for one-handed phone use first. Every interaction must be reachable, tappable, and fast on a 6-inch screen. Desktop is an enhanced version, not the other way around.

**4. Controlled Pressure**
The interface should create gentle urgency — due dates, incomplete tasks, ranking position — without causing anxiety. Like a good coach: firm but supportive.

**5. Premium by Default**
No generic components, no default browser styles, no lazy layouts. Every screen should feel intentionally designed. The bar is Apple/Notion-level craft, adapted to Arabic educational context.

---

### Technical Design Constraints

- **Stack:** Next.js 16 + TypeScript + Tailwind CSS 4 + Framer Motion + Zustand + Shadcn
- **Direction:** RTL (Arabic-first)
- **Font:** Cairo (Google Fonts) — weights 300–900
- **Accessibility:** WCAG AA compliance
  - Minimum 4.5:1 contrast ratio for text
  - Large, tappable buttons (min 44px touch targets)
  - Readable font sizes (min 14px body text)
  - Support `prefers-reduced-motion` — disable animations gracefully
  - No motion-dependent interactions
- **Performance:** Fast initial load, minimal JavaScript for student-facing pages
- **Device Priority:** Mobile > Tablet > Desktop (students) / Desktop > Mobile (admin/assistant)

---

### Existing Design Tokens (from DESIGN.md & globals.css)

**Surface Hierarchy (Light):**
```
Base:          #faf2e6  (--background)
Card:          #fcf6ea  (--card)
Muted:         #f0e4ce  (--muted)
Secondary:     #f2dfbc  (--secondary)
Sidebar:       #f7ecda  (--sidebar)
```

**Primary Gold Scale:**
```
Primary:       #9a6933
Strong:        #7f5427  (darker gold)
Container:     #c5a059  (lighter gold)
Foreground:    #fffaf1  (text on gold)
```

**Text:**
```
On-Surface:    #2c1708  (primary text — warm near-black)
Muted:         #7a644d  (secondary text)
Tertiary:      #485e8b  (links, subtle accents)
```

**Rules:**
- No 1px solid borders — use tonal shifts.
- No pure black (#000000) — use #2c1708.
- No standard blue links — use tertiary (#485e8b) or primary gold.
- Shadows always warm-tinted, never pure black.
- Glassmorphism: 60% opacity + 24px backdrop blur on floating elements.
- Button gradients: 135° from primary-strong to primary.
- Card corners: xl (1.5rem) or lg (1rem) — soft and approachable.
