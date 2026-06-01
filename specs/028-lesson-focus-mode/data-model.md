# Data Model: Lesson Focus Mode

*Note: This feature primarily deals with frontend state management and layout visibility, rather than persistent backend database entities.*

## Global Layout State

The single source of truth for whether the application UI is in "Focus Mode" rests in a Zustand store.

### `SidebarStore` / `LayoutStore`

- **`isFocusMode`** (`boolean`): Determines if the global `StudentSidebar` and `Navbar` components should be completely hidden from the viewport.
- **`toggleFocusMode()`** (`function`): Reverses the current `isFocusMode` state.
- **`setFocusMode(value: boolean)`** (`function`): Explicitly sets the focus mode state.

**Relationships**:
- Consumed by `TopNavbar`, `Sidebar` components (to unmount or collapse out of view via Framer Motion).
- Altered by `LessonViewer` and `SecureVideoPlayer` hooks upon mount/demount or via an explicit toggle switch button.
