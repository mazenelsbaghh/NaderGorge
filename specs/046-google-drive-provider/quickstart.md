# Quickstart: Google Drive Video Provider

This guide outlines how to use and test the new Google Drive video provider integration in the local environment.

## 1. Local Setup

1. No additional infrastructure or Docker containers are required for this feature. It operates solely via frontend routing and backend business logic.
2. Ensure your backend and frontend are running (`make start` or `npm run dev` / `dotnet run`).

## 2. Testing the Admin Upload Flow

1. Upload any test video to a Google Drive account.
2. Right-click the file in Google Drive, select "Share", and change General Access to "Anyone with the link". Use the "Viewer" role.
3. Copy the link.
4. Navigate to the Admin Panel in your local instance -> Lessons -> Edit/Create Video.
5. Select **Google Drive** from the provider dropdown.
6. Paste the copied URL into the Provider Video ID field. The frontend should automatically parse and keep only the `fileId`.
7. Save. Verify there are no errors.

## 3. Testing the Student Playback Flow

1. Log in as a student enrolled in the course that contains your new Google Drive video.
2. Navigate to that lesson's view page.
3. The video should load natively in the Google Drive iframe player.
4. Open your browser's DevTools:
   - Ensure the `API` calls fetch the `embed` endpoint.
   - Inspect the DOM to verify the `<iframe src="https://drive.google.com...">` is safely isolated inside a Shadow Root and the direct Drive link is not easily accessible via standard `src` attributes in the light DOM.

## 4. Notes

- Google Drive videos currently do not support AI processing (Mindmaps/Chapters). The AI dashboard should ignore them or show them as unsupported.
