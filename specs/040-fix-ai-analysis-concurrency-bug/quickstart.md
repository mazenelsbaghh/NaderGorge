# Quickstart & Verification

To verify that the `DbUpdateConcurrencyException` has been resolved:

1. Connect to the local database and verify `lesson_videos` and `video_chapters` are present.
2. Ensure the node worker `bullmq` and the .NET Backend are running locally.
3. Queue an AI chaptering job for a video using the frontend admin panel or a cURL request.
4. Let the AI job complete, which sends the `AiAnalysisCompletedCommand` webhook to the `.NET` backend.
5. In your `.NET API` terminal, observe the success response metric (200 OK) without any exceptions.
6. Refresh the video lesson on the frontend to manually witness the generated chapters showing successfully.
