# Progress Tracking Contract Data

`GET /api/status/{videoId}`

```json
{
  "id": "6fc77903-c72e-4b2b-94c5-857d54ece86f",
  "state": "active",
  "progress": {
    "percentage": 15,
    "stage": "Extracting Audio Track..."
  },
  "failedReason": null
}
```

The frontend UI maps `progress.stage` to specific Arabic text, or the worker can send the Arabic text directly.
Given the specification, the worker will send Arabic strings natively to be displayed instantly without translation maps on the client.

- **10%**: `جاري استخراج وتحضير الصوت من الفيديو...` (Extracting Audio)
- **40%**: `الذكاء الاصطناعي يقوم بتحليل وتلخيص المحتوى (قد يستغرق دقائق)...` (AI Generation)
- **85%**: `جاري بناء هيكل الفصول وإنشاء الترجمة...` (Formatting Subtitles)
- **95%**: `جاري حفظ الفصول وتحديث قواعد البيانات...` (Webhooks & DB Sink)
- **100%**: `اكتملت المعالجة بنجاح.` (Complete)
