# Quickstart: Live Support AI Refinements

## Running the Application Locally

1. **Start infrastructure services**:
   ```bash
   make up
   # Or run manually: docker compose up -d nadergorge_db nadergorge_redis
   ```

2. **Run the backend API**:
   ```bash
   cd backend
   dotnet run --project src/NaderGorge.API
   ```

3. **Run the frontend**:
   ```bash
   cd frontend
   npm run dev
   ```

## Verifying the Features

1. Open your browser and navigate to `http://localhost:8738/admin/live-support/ai`.
2. Login as the seeded administrator (`01000000000`/`Admin@123`).
3. Under the **الإعدادات وقاعدة القرار** (Settings) tab:
   - Try toggling the assistant off/on. You should see the active green pulse indicator badge appear when enabled.
   - Publish a new draft and ensure it succeeds with no 500 error.
4. Click on the **الإحصائيات والأداء** (Statistics) tab:
   - Verify the counters load.
   - Change the period dropdown filter and check that counts refresh.
