# API Contracts

## 1. Trigger AI Generation (Admin API)
`POST /api/V1/admin/lessons/{lessonId}/videos/{videoId}/generate-ai`
Role: Admin/Teacher

Initiates the background job via BullMQ.
**Response**:
```json
{
  "status": "Accepted",
  "message": "AI analysis job has been queued.",
  "jobId": "bullmq-123456"
}
```

## 2. Callback from Node Worker (Internal Webhook)
`POST /api/V1/internal/callbacks/ai-analysis-completed`
Role: Internal (Secured via Shared Secret Header)

The Node worker hits this endpoint to update the DB once the Gemini API completes.
**Request Body**:
```json
{
  "videoId": "guid",
  "subtitleUrl": "https://storage/bucket/path/to/subtitle.srt",
  "chapters": [
    {
      "title": "مقدمة الدرس",
      "startTime": 0,
      "endTime": 120,
      "summaryText": "شرح عن أهمية الفصل ومعلومات عامة.",
      "order": 1
    }
  ],
  "error": null
}
```

## 3. Lesson Video DTOs (Student API)
`GET /api/v1/lessons/{lessonId}`

The video response model is extended:
```json
{
  "id": "guid",
  "provider": "Telegram",
  "externalVideoId": "https://t.me/c/....",
  "subtitleUrl": "https://path/to/subtitle.srt",
  "chapters": [
    {
      "id": "guid",
      "title": "مقدمة الدرس",
      "startTime": 0,
      "endTime": 120,
      "summaryText": "شرح عن أهمية الفصل ومعلومات عامة.",
      "order": 1
    }
  ]
}
```
