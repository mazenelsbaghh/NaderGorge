# Quickstart & Verification: Lesson Focus Mode

## 1. Verify Entry to Focus Mode

1. Navigate to the `Student Dashboard`.
2. Ensure you have purchased a testing package safely.
3. Access any standard lesson that contains videos (e.g., `LessonCarousel`).
4. **Validation**: The moment the `LessonViewer` fully mounts, the navigation sidebar on the right and the top navigation bar should seamlessly slide/fade out of view. The video and lesson tabs must take up the full screen width.

## 2. Verify Togglability (Exit Focus Mode)

1. While in the `LessonViewer`, observe the new "Exit Focus Mode" (or "Show Menu") floating button.
2. Click the button.
3. **Validation**: The sidebar and navbar should animate back into view. The main content should shrink slightly to accommodate the sidebar again.

## 3. Verify Demount Cleanup

1. If you navigated via the navigation components outside the `LessonViewer`, press the browser back button or any "Go Back" action.
2. **Validation**: The `isFocusMode` state should be cleared (`false`) so that other areas (like the Package Dashboard) do not incorrectly inherit the focus mode state and appear broken. The menus must restore automatically.
