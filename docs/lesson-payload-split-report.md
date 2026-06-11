# Lesson Detail API Payload Sizing Report

This report provides an estimated payload model before and after partitioning `GetLessonDetailQuery` to load comments and resources separately.

> **Validation limit:** the values below are calculated from assumed item counts and approximate bytes per item. They are not captured HTTP response sizes from the running API. The model estimates an **88.7% reduction**, but the 60% success criterion remains unverified until measured with representative seeded data.

---

## 1. Scenario Parameters
We evaluate a typical, moderately-sized lesson containing:
* **Core Metadata**: Lesson title, summary, prices, blocking locks, etc.
* **10 Videos**: Each video has basic watch status data and 5 AI-generated chapters.
* **15 Resources**: Downloadable files (PDFs, worksheets, slides) containing titles and URLs.
* **100 Comments**: Moderate comment feed. Each comment has a body, reviewer metadata, and author details (ID, full name).
* **1 Homework**: Basic metadata and 5 homework questions.

---

## 2. Payload size comparison (in Bytes)

### A. Core Lesson Detail (Base Metadata, Videos, Homework)
```json
{
  "id": "c1f7b902-8e7c-47ea-a2f0-cf7b9028e7ca",
  "title": "Lesson 1: Introduction to Mechanics",
  "summary": "This lesson covers the fundamentals of Newton's laws.",
  "packageId": "b1f7b902-8e7c-47ea-a2f0-cf7b9028e7cb",
  "examId": null,
  "homework": {
    "id": "d1f7b902-8e7c-47ea-a2f0-cf7b9028e7cc",
    "title": "Homework 1",
    "instructions": "Answer all questions",
    "isMandatory": true,
    "requiredPointsToPass": 4.0,
    "totalScore": 10.0,
    "questions": [
      { "id": "e1f7b902-8e7c-47ea-a2f0-cf7b9028e7cd", "text": "Question 1", "order": 1, "maxPoints": 2 }
      // 4 more questions... (approx. 500 bytes total)
    ]
  },
  "videos": [
    {
      "id": "f1f7b902-8e7c-47ea-a2f0-cf7b9028e7ce",
      "title": "Part 1: Velocity and Acceleration",
      "provider": "youtube",
      "order": 1,
      "limit": 3,
      "watched": 1,
      "isLocked": false,
      "watchedSeconds": 360,
      "lastWatchedAt": "2026-06-11T12:00:00Z",
      "subtitleUrl": null,
      "isProcessingAI": false,
      "isProcessingMindmaps": false,
      "examId": null,
      "examPassed": false,
      "isExamLocked": false,
      "chapters": [
        { "id": "a1f7b902-8e7c-47ea-a2f0-cf7b9028e7cf", "title": "Introduction", "startTime": 0, "endTime": 60, "summaryText": "Brief intro", "mindmapImageUrl": null, "order": 1 }
        // 4 more chapters... (approx. 600 bytes per video chapters list)
      ]
    }
    // 9 more videos... (approx. 7,000 bytes total)
  ],
  "isLocked": false,
  "lockedReason": null,
  "blockingExamId": null,
  "blockingHomeworkLessonId": null
}
```
* **Modeled Base Size**: **~8,100 bytes (8.1 KB)**

---

### B. Resources Payload (15 items)
Each resource contains an ID, Title, Type, and File URL (e.g. `uploads/lessons/pdf1.pdf`).
* **JSON Size per Resource**: ~250 bytes.
* **15 Resources**: 15 * 250 = **~3,750 bytes (3.75 KB)**

---

### C. Comments Payload (100 items)
Each comment contains an ID, Body, Status, CreatedAt, Author UserId, and Author FullName.
```json
{
  "id": "c1f7b902-8e7c-47ea-a2f0-cf7b9028e701",
  "body": "This is a sample comment with some discussion text about Newton's laws.",
  "status": "Approved",
  "createdAt": "2026-06-11T14:30:00Z",
  "authorUserId": "u1f7b902-8e7c-47ea-a2f0-cf7b9028e702",
  "authorFullName": "Ahmed Aly"
}
```
* **JSON Size per Comment**: ~600 bytes (longer bodies and full names).
* **100 Comments**: 100 * 600 = **~60,000 bytes (60.0 KB)**

---

## 3. Comparison Metrics

| Response Configuration | Size (KB) | Reduction % |
|---|---|---|
| **Before Split** (Core + Resources + Comments) | **71.85 KB** | *Baseline* |
| **After Split** (Core Lesson Detail Only) | **8.10 KB** | **88.73%** |

---

## 4. Validated And Pending Conclusions
* **Validated in code**: comments and resources are no longer included in the initial lesson-detail DTO and are fetched through separate endpoints.
* **Estimated**: under the scenario assumptions, the initial payload falls from **71.85 KB** to **8.10 KB**, an **88.73% modeled reduction**.
* **Still required**: seed a representative lesson, capture the actual compressed and uncompressed HTTP response sizes before/after the split, and record timings over a controlled network profile.
