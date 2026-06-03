# Quickstart: Removing Bunny and Telegram Video Providers

## Local Setup
1. Apply the database migrations:
   ```bash
   make migrate
   ```
2. Build and run the development environment:
   ```bash
   make dev
   ```

## Verification Checks
1. Go to Admin Panel -> Content Management -> Add Video.
2. Ensure only "YouTube" and "VK" are in the dropdown.
3. Go to Student Dashboard and watch a migrated video. Verify it plays from YouTube successfully.
