# Contract: Assessment Controls And Question Media

## Admin Question Image Upload

`POST /api/admin/questions/image`

Request:

- `multipart/form-data`
- Field: `image`
- Accepts image MIME types only.

Success response:

```json
{
  "success": true,
  "data": {
    "url": "/uploads/content/questions/example.webp"
  }
}
```

Failure response:

```json
{
  "success": false,
  "message": "Invalid image file."
}
```

## Inline Exam Question Payload

Each question accepts optional `imageUrl`:

```json
{
  "text": "<p>Question text</p>",
  "type": "MCQ",
  "points": 1,
  "order": 1,
  "imageUrl": "/uploads/content/questions/q1.webp",
  "options": [{ "text": "A", "isCorrect": true }]
}
```

## Homework Question Payload

Each question accepts optional `imageUrl` using the same shape as inline exam questions.

## Student Question Payload

Exam and homework attempt/review question responses include:

```json
{
  "text": "<p>Question text</p>",
  "imageUrl": "/uploads/content/questions/q1.webp"
}
```
