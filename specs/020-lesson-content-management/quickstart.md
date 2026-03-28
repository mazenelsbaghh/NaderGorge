# Setup & Execution

Since the project uses Next.js and .NET 8 Web API, there's no major infrastructure change. You only need to verify that your C# and Node environments are running up-to-date versions.

Steps:
1. Rebuild the API project: `dotnet build backend/src/NaderGorge.API`
2. Run database migrations: NO schema changes are required as `LessonVideo`, `LessonResource`, `Homework`, and `ExamId` are already in the Context.
3. Start API backend: `make dev` or `dotnet run` inside the backend API directory.
4. Launch the Next.js React frontend: `npm run dev` inside `frontend/src`.
5. Access the Admin Panel at `/admin/content/lessons/[id]` to test the new cockpit interface.
