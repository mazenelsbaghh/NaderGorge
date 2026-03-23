# 02 — Content Blueprint

## Hierarchy Definition
The educational material is strictly organized into a 3-tier hierarchy:
1. **Package:** The highest level container. This is what a student purchases or unlocks (e.g., "Secondary 3 - First Term - Physics").
2. **Content Section:** A thematic or temporal grouping within a package (e.g., "Chapter 1: Mechanics" or "September Content").
3. **Lesson:** The atomic unit of learning where the actual educational content lives.

## Lesson Components
Every lesson is a composite entity that can contain any combination of the following components:
- **Videos:** Supports multiple videos per lesson (e.g., "Main Explanation", "Homework Solution", "Extra Examples").
- **Written Summary:** A text or PDF document summarizing the main points of the lesson.
- **Short Questions:** In-video or post-video single-question checks to ensure attention.
- **Homework:** A scored assignment (MCQ, Essay, or mixed) that must be passed to proceed.
- **Short Quiz:** A quick, scored assessment to evaluate immediate understanding.
- **Downloadable File:** Worksheets, cheat sheets, or external resources provided by the teacher.
- **Mind Map:** A visual diagram representing the lesson's concepts, crucial for the teacher's style.
- **Revision Block:** Specialized content intended for pre-exam review, distinct from the initial explanation.

## Content Section (Terminology Note)
- **Canonical Term:** The system internally and externally models this strictly as a `ContentSection`. This is the term used in API contracts, database schemas, and the standard UI.
- **Internal Alias:** Historically and operationally, the academy refers to these groupings as "months" (e.g., "Month 1", "Month 2"). This alias is documented here for business context but must *not* be used in the technical data model or core API to ensure flexibility (as a "Content Section" could represent a chapter, a week, or a specific topic, not just a calendar month).

## Package Structure
A Package is designed to be a comprehensive unit of study.
- A Package contains **multiple Content Sections**.
- Each Content Section contains **multiple Lessons**.
- Packages can be sold individually, or multiple packages can be unlocked via a Term Code. The system must support flexible association between Packages and the code system.
