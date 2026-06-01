# Research & Outline: Video Carousel Navigation

## Decision 1: Carousel Component Integration

**Decision**: Integrate the `cult-ui` FeatureCarousel manually rather than via CLI, and refactor its image rendering logic to comply with the project's rendering strictness guidelines.

**Rationale**: 
The user specifically requested the `cult-ui` FeatureCarousel component. However, third-party motion components often utilize `next/image` (`<Image />`) heavily. According to **Constitution Principle XI (Frontend Reliability & Rendering Strictness)**, Next.js `<Image fill />` components are strictly forbidden inside `framer-motion` containers that manipulate scale or height from 0, because it causes Next.js hydration `height: 0` bugs and Flash of Unstyled Content (FOUC). By manually copying the source, we can replace all `<Image />` tags with the mandated native `<img className="absolute inset-0 h-full w-full object-cover" onError={...} />` fallback loop breaker.

**Alternatives considered**: 
- Running `npx shadcn@latest add https://cult-ui.com/r/feature-carousel.json`. Rejected because the downloaded component would likely violate Principle XI and require extensive modification anyway.
- Building a custom carousel from scratch. Rejected because the user specifically requested "this animation" referring to the `cult-ui` implementation.

## Decision 2: Video Player State Syncing

**Decision**: The `FeatureCarousel` must be lifted or controlled so that its internal `currentStep` state is synchronized with a parent `activeVideoId` state. 

**Rationale**:
The lesson page needs to play the active video. The `FeatureCarousel` acts as a playlist selector. When a video finishes naturally in the player, the parent component must increment the `activeVideoIndex` and pass it down as a prop to the `FeatureCarousel` so it animates to the next video visually without requiring user clicks.

**Alternatives considered**:
- Let the carousel manage its own state. Rejected because the video player (external to the carousel) needs to know what video to play, and must be able to change the carousel step when auto-playing the next video.

## Decision 3: Styling Strictness

**Decision**: Remove any arbitrary Tailwind values (e.g., `w-[325px]`) from the component's source code and replace them with standard Tailwind standard tokens (e.g., `w-80`) or inline style overrides.

**Rationale**:
Constitution Principle XI mandates avoiding arbitrary Tailwind structural values due to Turbopack JIT compilation drops observed in this project. All custom offset layouts required by the `cult-ui` component must be converted to standard classes or inline styles.
