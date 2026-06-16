# Research Notes: Audio Upload Restrictions & Review Display

## Decisions & Rationale

### 1. Backend Audio Saving
- **Decision**: Save uploaded audio files to `wwwroot/uploads/audio/` using `Directory.GetCurrentDirectory()` as the base directory.
- **Rationale**: The Application layer (`UploadQuestionAudioCommandHandler`) does not have access to ASP.NET Core's `IWebHostEnvironment` without introducing clean-architecture coupling. Using `Directory.GetCurrentDirectory()` is a standard pattern in this codebase (already used in `PublicController.cs`) that resolves to the root web folder seamlessly.
- **Alternatives considered**: Injecting `IWebHostEnvironment` into the Application layer (rejected as it violates clean architecture layering principles).

### 2. Backend Validation (Only Audio Allowed)
- **Decision**: Enforce validation in both `AdminController.UploadQuestionAudio` (for questions) and the new `StudentController.UploadStudentAudio` (for answers).
- **Rationale**: Check the file's `ContentType` to ensure it starts with `audio/` and check the file extension against a whitelist of valid audio formats (`.mp3`, `.wav`, `.m4a`, `.webm`, `.ogg`, `.aac`, `.amr`, `.flac`). This prevents spoofing attempts (e.g. renaming `.txt` to `.mp3`) and satisfies the requirement "ماينفعش ارفع غير ملفات صوت" (cannot upload anything other than audio).

### 3. Exposing Student Exam Audio URLs
- **Decision**: Extend `QuestionReviewSnapshot` and `ExamQuestionReviewDto` to include `StudentAudioUrl`.
- **Rationale**: Currently, exam result builders completely ignore `essay.AudioUrl`. Exposing this field in the review DTOs allows the frontend client (`ExamViewer.tsx`) to render the student's voice answers.
- **Alternatives considered**: Querying `EssaySubmission` separately in the frontend (rejected because it adds extra round-trips and complicates client-side rendering).

### 4. Rendering Student Homework Audio Answers
- **Decision**: Check if `HomeworkAnswer.ProvidedAnswer` starts with `/uploads/audio/`.
- **Rationale**: For homework, essay answers are saved as a text string in `ProvidedAnswer`. If the student uploads an audio file as their solution, the file URL is saved directly in `ProvidedAnswer`. Checking if it is an audio URL and rendering the `<audio>` player is a lightweight, clean approach that does not require database migrations.
