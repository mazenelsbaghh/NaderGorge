# Research Output: Papyrus Package UI

## Investigation: Papyrus Paper Styling Approach

**Decision**: We will implement the papyrus paper look using a dual approach: CSS for layout and an overlay background image for the texture/color.
**Rationale**: A pure CSS approach (using gradients and drop shadows) can mimic a scroll, but a real papyrus texture requires an image for authenticity. Using Tailwind CSS, we can combine a base color (e.g., `#E8D5A6`), a subtle noise/texture background image (`bg-[url('/images/papyrus-texture.png')]`), and torn-edge SVG masks or `border-radius` tricks to create a highly performant and responsive scroll look.
**Alternatives considered**: Pure CSS. Rejected because it lacks the organic feel required for the pharaonic theme.

## Investigation: Default Image Fallback

**Decision**: We will utilize a high-quality default image located in `public/images/default-package.png` for any package that does not have an explicit `ImageUrl` defined in the database.
**Rationale**: We need to guarantee that every package card renders perfectly inside the papyrus layout without empty holes. A dedicated, themed default image (perhaps showing an ancient Egyptian scroll, the academy logo, or a pharaoh) maintains the high-end feel of the platform.
**Alternatives considered**: Hiding the image slot when empty. Rejected because the feature explicitly requires an image to always be present to maintain structural consistency.
