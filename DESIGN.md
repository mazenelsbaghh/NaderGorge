# Design System Document - Massar Academy Digital System

## 1. Creative North Star

Massar Academy's interface is a structured learning path. The brand is not a luxury archive and not a generic SaaS dashboard. It is a confident educational system that makes progress visible through steps, route lines, dotted grids, growth marks, and clear academic hierarchy.

The visual promise is simple: **every student has a path, every path has a next step, and every step moves upward.**

---

## 2. Color System

### Core Colors

| Role | Name | Hex | Use |
|---|---|---:|---|
| Primary | Deep Navy | `#0A1D3D` | Brand authority, headings, nav, primary text, major surfaces |
| Accent | Teal | `#0E8F8F` | Progress, active states, icons, route lines, links |
| Achievement | Warm Gold | `#D4A017` | Milestones, graduation cues, small separators, premium emphasis |
| Canvas | Off White | `#F6F7F8` | App and marketing backgrounds |
| Text Secondary | Dark Gray | `#2E3A47` | Body/supporting copy |
| Line | Light Gray | `#DCE1E6` | Quiet boundaries and disabled outlines |
| Surface | Soft Gray | `#EEF1F4` | Secondary panels, inactive fills, muted backgrounds |
| White | White | `#FFFFFF` | Cards and high-contrast logo applications |

### CSS Token Mapping

```css
:root {
  --background: #F6F7F8;
  --foreground: #0A1D3D;
  --card: #FFFFFF;
  --card-foreground: #0A1D3D;
  --primary: #0A1D3D;
  --primary-foreground: #FFFFFF;
  --secondary: #0E8F8F;
  --secondary-foreground: #FFFFFF;
  --accent: #D4A017;
  --accent-foreground: #0A1D3D;
  --muted: #EEF1F4;
  --muted-foreground: #2E3A47;
  --border: #DCE1E6;
  --ring: #0E8F8F;
}
```

### Usage Rules

- Deep Navy should carry the most visual weight.
- Teal should signal movement, progress, interactivity, and selected states.
- Warm Gold should be rare and meaningful: achievement, separators, graduation tassel cues, celebration moments.
- Off White is the default canvas. Avoid warm cream/sand palettes from the previous identity.
- Do not use purple, neon cyan, or saturated gradients.
- Do not use standard blue links. Use Teal.
- Use Dark Gray for secondary text, not washed-out low-contrast gray.

---

## 3. Typography

### Font Families

- Arabic headings: **Tajawal Bold**
- Arabic body: **Tajawal Regular**
- English headings: **Montserrat Bold**
- English body: **Montserrat Regular**

### Type Scale

| Role | Font | Size | Weight | Use |
|---|---|---:|---|---|
| Heading | Tajawal | 36-40px | 700-800 | Page titles, hero statements, major dashboards |
| Subheading | Tajawal | 20-24px | 500-700 | Section headers and primary cards |
| Body | Tajawal | 14-16px | 400 | Main content and descriptions |
| Caption | Tajawal | 11-12px | 400-500 | Metadata, helper text, minor labels |

### Typography Rules

- Arabic-first surfaces use Tajawal throughout.
- English fragments, marketing labels, or bilingual captions use Montserrat.
- Headings should be navy. Teal may emphasize one key word or progress phrase.
- Avoid negative letter spacing in Arabic.
- Keep body line length comfortable. Long Arabic paragraphs should not exceed 65-75 characters per line.

---

## 4. Visual Language

### Brand Patterns

Use these as structural motifs:

- Steps and stair forms.
- Dotted progress grids.
- Route lines and dashed paths.
- Upward arrows and growth bars.
- Circular arcs for continuous learning.
- Small gold separators under headings.

### Pattern Rules

- Patterns must support progress, hierarchy, or wayfinding.
- Dots and route lines should be low contrast and quiet.
- Stair shapes can frame empty states, onboarding, lesson progress, or achievement summaries.
- Avoid decorative blobs, gradient orbs, and abstract bokeh.

### Iconography

Icons should be clean line icons with consistent stroke. Preferred semantic set:

- تعلم / graduation cap
- معرفة / book
- فكرة / lightbulb
- هدف / target
- تطور / growth chart
- طالب / user
- ثقة / shield
- وقت / clock
- إبداع / pencil
- عالم / globe

Use Teal for most icons, Deep Navy for active authority states, and Warm Gold for achievement details only.

### Photography

Photo style: optimistic, clean, youthful, and inspiring. Students should feel ambitious and focused. Classrooms and study environments should be bright and organized. Brand overlays may use teal stair lines, dots, or small flags.

---

## 5. Layout And Surfaces

### Surface Hierarchy

```text
Canvas:          #F6F7F8
Card:            #FFFFFF
Muted panel:     #EEF1F4
Quiet line:      #DCE1E6
Authority block: #0A1D3D
Progress accent: #0E8F8F
Achievement:     #D4A017
```

### Layout Rules

- Use generous whitespace, but keep task flows compact enough for mobile study sessions.
- Student screens should guide the eye from current status to next action.
- Admin screens can be denser, but must still use clear grouping and readable tables.
- Prefer structural separators: spacing, soft gray panels, and navy/teal section anchors.
- Thin borders are allowed when they improve clarity, but avoid heavy boxed-in layouts.
- Rounded corners should be moderate: 12-20px. Avoid overly pill-shaped cards unless the control is a chip or CTA.

---

## 6. Components

### Buttons

- Primary: Deep Navy background, white text.
- Primary active/progress variant: Teal background, white text.
- Achievement CTA: Warm Gold accent only for completion, success, rewards, or graduation moments.
- Secondary: White or Soft Gray background with navy text.
- Ghost: transparent with teal or navy text and soft gray hover.
- All buttons must be at least 44px high on mobile.

### Cards

- Use white cards on Off White canvas.
- Use Soft Gray for nested or secondary panels.
- Do not stack cards inside cards unless the nested element is a real repeated item.
- Highlight progress with a teal line, step marker, or growth indicator rather than a decorative gradient.

### Tables

- Header rows can use Soft Gray with navy labels.
- Row hover can use `#EEF1F4`.
- Use subtle `#DCE1E6` separators only when density requires them.
- Clickable rows must be keyboard accessible and announced as buttons or links.

### Forms

- Inputs use white or Soft Gray fill, navy text, Dark Gray placeholder.
- Focus state: 2px Teal ring.
- Error state: clear red only for actual errors.
- Required indicators must be text or accessible markers, not color alone.

### Progress And Status

- Progress bars, current steps, active tabs: Teal.
- Completed milestones: Warm Gold or Teal depending on context.
- Locked/inactive: Soft Gray with Dark Gray text.
- Dangerous/destructive states: use red sparingly and consistently.

---

## 7. Motion

- Motion should communicate progress, reveal, loading, or completion.
- Use 150-250ms transitions for product UI.
- Avoid bounce and elastic movement.
- Do not animate layout properties like width, height, top, left, padding, or margin when transform/opacity can work.
- Respect `prefers-reduced-motion`.
- Heavy canvas/WebGL effects are only acceptable on marketing or landing surfaces, and must pause offscreen.

---

## 8. Logo Rules

- Use the primary logo on light backgrounds.
- Use the reversed white logo on Deep Navy backgrounds.
- Use the monochrome logo only when reproduction demands it.
- Do not change logo colors.
- Do not rotate the logo.
- Do not distort proportions.
- Do not add shadows or effects.
- Maintain clear space around the logo.
- Minimum size: 20mm equivalent for digital icon use, 25mm equivalent for print logo use.

---

## 9. Do And Do Not

### Do

- Use Deep Navy as the foundation.
- Use Teal to show movement and interactivity.
- Use Warm Gold for achievement and small brand separators.
- Use stair, route, dot, and growth motifs where they clarify progress.
- Keep Arabic copy direct and motivating.
- Make the next step obvious on every student surface.

### Do Not

- Do not keep the old gold/cream/pharaonic direction.
- Do not use generic purple/blue gradients.
- Do not use decorative glassmorphism as a default.
- Do not overload screens with unrelated icons.
- Do not use tiny touch targets.
- Do not make student flows feel like social feeds.
- Do not make admin tables inaccessible by using clickable rows without keyboard behavior.
