# Quickstart Guide: Media Production and Social Planner

## 1. Setting Up the Environment

Run the standard Docker setup command to verify the local services are running:
```bash
make up
```

Apply database migrations:
```bash
make migrate
```
*(Verify that `MediaProductionPipelines` and `SocialMediaPlans` tables are successfully created in the Postgres volume).*

---

## 2. Running Automated Tests

Run the newly created unit tests for the pipeline stage transitions and approval workflow hooks:
```bash
dotnet test backend/NaderGorge.sln --filter Category=Media
```

---

## 3. Testing APIs manually

You can mock and trigger the webhook or API endpoints using the following `curl` calls:

### Create a Media Pipeline Item:
```bash
curl -X POST http://localhost:5245/api/admin/media/pipelines \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your_jwt_token>" \
  -d '{
    "title": "Lesson 1: Introduction",
    "description": "Outlines and basic definitions",
    "assetFolderUrl": "https://drive.google.com/drive/folders/test1"
  }'
```

### Transition to Review (Triggers Task Creation):
```bash
curl -X PUT http://localhost:5245/api/admin/media/pipelines/<pipeline_id> \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your_jwt_token>" \
  -d '{
    "title": "Lesson 1: Introduction",
    "stage": "Review",
    "supervisorId": "<supervisor_user_id>"
  }'
```

### Approve Task (Reuses Task Resolution API):
```bash
curl -X POST http://localhost:5245/api/admin/operations/tasks/<task_id>/resolve \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <supervisor_jwt_token>" \
  -d '{
    "approve": true
  }'
```
*(Verify that the pipeline item stage automatically changes to `Approved` in the database).*
