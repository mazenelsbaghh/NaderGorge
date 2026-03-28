# Design System Document: The Editorial Scholar
## 1. Overview & Creative North Star
### The Creative North Star: "The Curated Archive"
This design system moves away from the sterile, "software-as-a-service" aesthetic toward a high-end, editorial experience. Inspired by premium academic journals and luxury curators, it treats information not as data to be managed, but as content to be showcased.
We achieve this through **Organic Intentionality**: a rejection of standard grids in favor of purposeful whitespace, sophisticated tonal shifts, and "soft-touch" digital surfaces. By using a palette of warm creams and burnished golds, the system fosters a sense of prestige and focus—essential for a high-end Student Area. The layout avoids "boxed-in" feelings by using asymmetrical breathing room and layered elevations that mimic the depth of fine stationary stacked on a desk.
---
## 2. Colors: Tonal Depth & Luster
The color palette is anchored in heritage tones: rich creams (`surface`), deep umbers (`on-surface`), and metallic-inspired golds (`primary`).
### The "No-Line" Rule
**Borders are an admission of failure in layout.** To maintain a premium, seamless feel, designers are prohibited from using 1px solid borders to define sections. Instead, boundaries must be established through:
- **Background Shifts:** Use `surface-container-low` for a sidebar against a `surface` main body.
- **Negative Space:** Use the Spacing Scale (specifically `8` to `12`) to separate functional groups.
### Surface Hierarchy & Nesting
Think of the UI as a series of nested cardstock.
- **Base Level:** `surface` (#fcf9ef)
- **Primary Containers:** `surface-container` (#f1eee4)
- **Focused Elements:** `surface-container-highest` (#e5e2d9)
Always place a higher-tier container on a lower-tier background to create "natural" depth without artificial strokes.
### The "Glass & Gradient" Rule
To add "soul" to the digital interface:
- **Glassmorphism:** Use `surface_variant` at 60% opacity with a `24px` backdrop blur for floating navigation bars or modal overlays.
- **Signature Gradients:** Main CTAs or active state backgrounds (like a selected student track) should utilize a subtle linear gradient from `primary` (#775a19) to `primary_container` (#c5a059) at a 135° angle.
---
## 3. Typography: The Manrope Monograph
We use **Manrope** exclusively for its geometric clarity and modern humanist feel. It bridges the gap between technical precision and editorial warmth.
- **Display (Lg/Md/Sm):** Reserved for high-level dashboard welcomes (e.g., "Welcome back, Alex"). Use `-0.02em` letter spacing to feel more compact and premium.
- **Headline (Lg/Md/Sm):** Used for section titles like "User Management." These should always use `on_surface` to establish a strong visual anchor.
- **Title (Lg/Md/Sm):** Used for card headers and navigation items. Pair `title-md` with `secondary` colors for a sophisticated, subdued hierarchy.
- **Body (Lg/Md/Sm):** The workhorse for student data. Use `body-md` for standard text and `body-sm` for metadata.
- **Label (Md/Sm):** Use exclusively for micro-copy, tags, and "all-caps" secondary markers to provide rhythmic contrast to the body text.
---
## 4. Elevation & Depth
In this design system, elevation is a feeling, not a drop-shadow.
- **The Layering Principle:** Stacking is our primary tool. A `surface-container-lowest` card placed on a `surface-container-low` background provides a soft "lift."
- **Ambient Shadows:** When an element must float (e.g., a dropdown or a primary action card), use an extra-diffused shadow: `box-shadow: 0 12px 40px rgba(78, 70, 57, 0.08)`. Notice the shadow is tinted with a warm umber—never pure black.
- **The "Ghost Border" Fallback:** If a border is required for high-contrast accessibility, use `outline_variant` at **15% opacity**. It should be felt, not seen.
- **Glassmorphism:** Use for floating elements like the "Student Area" header. This ensures the gold accents bleed through the interface, keeping the experience cohesive.
---
## 5. Components
### Buttons
- **Primary:** Gradient fill (`primary` to `primary_container`), `full` roundedness, and `title-sm` typography.
- **Secondary:** `surface-container-highest` background with `primary` text. No border.
- **Tertiary/Ghost:** Pure text using `primary` color, with a background shift to `surface-variant` only on hover.
### Cards & Tables (The "Lineless" Table)
- **Forbid Dividers:** Do not use lines between table rows or card sections.
- **Styling:** Use a `surface-container-low` background for the header row. Use `surface-container-lowest` for the body. Distinguish rows by a 4px vertical gap (Spacing `1`) and a subtle color change on hover.
- **Roundedness:** All cards must use `xl` (1.5rem) or `lg` (1rem) corners to feel approachable and soft.
### Inputs & Search
- **Search Bar:** Use `surface-container-highest` with `full` roundedness. The placeholder text should use `outline` color for a "recessed" look.
- **Focus State:** Instead of a thick border, use a 2px outer glow of `primary_fixed` at 50% opacity.
### Chips (Status Markers)
- **Design:** Use `secondary_container` for background and `on_secondary_container` for text. `full` roundedness is mandatory.
- **Context:** Perfect for "Active," "Pending," or "Graded" statuses in the Student Area.
---
## 6. Do's and Don'ts
### Do
* **Do** use asymmetrical margins. A wider left margin on a dashboard title creates an "editorial" entry point.
* **Do** use tonal shifts (e.g., `surface` to `surface-dim`) to separate the sidebar from the main content.
* **Do** embrace large typography scales for empty states to make them feel like a design choice rather than a "missing" page.
* **Do** ensure `on-surface` text maintains a 4.5:1 contrast ratio against the cream backgrounds.
### Don't
* **Don't** use 1px solid #CCCCCC or #000000 borders. Ever.
* **Don't** use pure black (#000000) for text. Use the provided `on_surface` (#1c1c16).
* **Don't** cram elements together. If a section feels crowded, double the spacing token (e.g., move from `4` to `8`).
* **Don't** use standard "Blue" for links. Use the `tertiary` (#485e8b) or `primary` (#775a19) tones to maintain the gold/cream warmth.