# Quickstart: Implementing Papyrus Package UI

This guide outlines the immediate steps to take to implement the Papyrus styling on Package cards.

## Prerequisites

- **Images**: Ensure you have two key assets:
  1. `public/images/papyrus-texture.png` (or a similar seamless texture for the card background).
  2. `public/images/default-package.png` (a fallback image showing an ancient or pharaonic theme).

## Implementation Steps

1. **Locate the Package Card Component**:
   Navigate to the frontend directory and find the component responsible for rendering the package items (likely `PackageCard.tsx` under `src/components/packages/` or similar).
   
2. **Apply the Background Texture**:
   Update the container `div`'s classes to include Tailwind utility classes for the papyrus base color and the background image. You may also want to use a torn-paper SVG mask or layered pseudo-elements (`::before`/`::after`) to make the edges look authentic.

3. **Enforce Image Display**:
   Locate the `<Image />` component or `<img>` tag rendering the package image. Modify it to use a fallback if the package object's `imageUrl` is absent. For example:
   ```tsx
   <Image src={pkg.imageUrl || '/images/default-package.png'} alt={pkg.title} />
   ```

4. **Adjust Text Color (Contrast)**:
   Since the papyrus texture will likely have a light golden/beige hue, ensure all textual elements inside the card use strong, dark colors (e.g., `#2C1E16`, `text-zinc-900`) instead of any light mode defaults, to maintain standard accessibility.

5. **Test the Layout**:
   Load up the responsive layout across various screen widths and confirm that the image scaling behaves well within the new papyrus-styled container.
