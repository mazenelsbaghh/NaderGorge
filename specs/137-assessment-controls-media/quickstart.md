# Quickstart: Assessment Controls And Question Media

1. Run backend tests:
   ```bash
   dotnet test backend/NaderGorge.sln
   ```

2. Run frontend lint:
   ```bash
   cd frontend && npm run lint
   ```

3. Manual admin check:
   - Open a lesson admin page.
   - Create homework with mandatory disabled.
   - Create lesson exam with mandatory enabled.
   - Create video part exam with mandatory disabled.
   - Add a question image in the shared question editor.

4. Manual student check:
   - Start the homework and exam.
   - Confirm question image is visible.
   - Confirm raw `<p>` tags are not visible.
   - Confirm optional assessments do not block the next lesson.
