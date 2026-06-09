# Quickstart Guide: Testing 5-Surface Domain Separation

## Running Locally

To run the entire platform with 5 frontend surfaces, build and run using Docker Compose:

```bash
# Build the images (Next.js image builds once and is shared across 5 frontend containers)
docker compose build

# Start the services
docker compose up -d
```

## Local Port Mappings

Once running, access the surfaces directly via their mapped ports:
- Public Landing Page: [http://localhost:8738](http://localhost:8738)
- Student Surface: [http://localhost:8739](http://localhost:8739)
- Admin Panel: [http://localhost:8740](http://localhost:8740)
- Teacher Surface: [http://localhost:8741](http://localhost:8741)
- Assistant/Staff Surface: [http://localhost:8742](http://localhost:8742)

## Domain Verification Script

To verify that the domains, redirects, and CORS headers are correctly isolated:

```bash
# Run the verification script
node scripts/verify-surface-separation.mjs
```
