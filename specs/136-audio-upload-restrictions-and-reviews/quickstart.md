# Quickstart & Verification Guide: Audio Upload Restrictions

## Running the Project Locally

To run the backend API and frontend:
1. Ensure Docker containers are running:
   ```bash
   docker compose up -d
   ```
2. Run frontend dev server:
   ```bash
   cd frontend && npm run dev
   ```

## Verification Steps

### 1. Verification of Backend Restriction
Send a POST request with a non-audio file (e.g., text/PDF) to the new student audio upload endpoint `/api/Student/upload-audio` and verify it returns a `400 Bad Request` with an error message in Arabic or English:
- "Only audio files are allowed" or "Invalid audio file extension".

### 2. Verification of Audio Review Panels
- Solve homework containing an Essay question, upload an audio file as the answer, and submit.
- Solve an exam containing an Essay question, upload an audio file, and submit.
- Go to the reviews page and verify the HTML5 audio players are rendered and function correctly.
