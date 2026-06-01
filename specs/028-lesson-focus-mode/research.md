# Research: Lesson Focus Mode

## 1. Hiding Global App Navigation in Next.js App Router

**Decision**: Use a global `Zustand` store (e.g., `useLayoutStore`) to manage the visibility of the global `Navbar` and `Sidebar` from deep within the `LessonViewer` component, OR simply apply a specific layout to the `lessons/[lessonId]` route if the global nav is nested above it. Since Nader George App Router currently renders the global sidebar/header in a high-level `layout.tsx` (e.g., `app/student/layout.tsx`), we will use `Zustand` to trigger a `fullScreen` or `focusMode` state, preventing full page reloads and allowing seamless toggles without routing changes.

**Rationale**: 
1. The user requested the ability to toggle the navbar/sidebar back safely (User Story 2). If we used a bare Next.js `layout.tsx` that strictly stripped the header, the student would have to navigate away to bring it back. A Zustand state `isFocusMode` allows the global layout to dynamically fold/hide the sidebar and navbar while preserving the React tree and video state.
2. It adheres to the Constitution's mandate for smooth, premium UI experiences.

**Alternatives considered**:
- **Next.js Parallel Routes / Intercepting Routes**: Overly complex for a simple layout toggle.
- **Native Browser Fullscreen API**: Doesn't hide the sidebar explicitly if the user just wants the browser window's normal "focus" without being forced into actual OS-level fullscreen (though native fullscreen can be an added bonus).
- **CSS-only hiding via path matching**: Brittles easily if routes change, and harder to animate seamlessly using Framer Motion compared to React state.

## 2. Animation Strategy for Layout Shifts

**Decision**: Use Framer Motion (`<AnimatePresence>` or `motion.div` with layout transitions) in the global layout components to slide out the Navbar and Sidebar.

**Rationale**: The Constitution (Principle VIII) heavily favors Framer Motion for premium, smooth transitions instead of harsh display toggles.
