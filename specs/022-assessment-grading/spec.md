# Feature Specification: Assessment Grading

**Feature Branch**: `022-assessment-grading`  
**Created**: 2026-03-28  
**Status**: Draft  
**Input**: User description: "لازم احط درجه نهائيه للامتحان ودرجه نجاح ده للواجب و للامتحان و انت اعملي التقيدرات"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Defining Assessment Grading Rules (Priority: P1)

Admins must be able to specify the absolute Total Score (الدرجة النهائية) and Passing Score (درجة النجاح) when creating or editing any Homework or Exam independently of the sum of the individual question points.

**Why this priority**: It is the foundational requirement to evaluate a student's performance correctly. Without knowing the final target score and passing threshold, no evaluation can happen.

**Independent Test**: Can be fully tested by creating a Homework/Exam via the Admin Cockpit, inputting "Total Score = 100" and "Passing Score = 50", and verifying these exact bounds are saved to the database.

**Acceptance Scenarios**:

1. **Given** the admin is creating an Exam/Homework, **When** they fill the form, **Then** they must find explicitly required fields for "Total Score" and "Passing Score".
2. **Given** an assessment with Total Score = 50, **When** an admin tries to set Passing Score = 60, **Then** the system should reject it (Passing Score cannot exceed Total Score).

---

### User Story 2 - Automated Evaluation and Grading Scale (Priority: P1)

The system automatically calculates the student's textual Evaluation (التقدير) based on the percentage of their earned score relative to the Total Score, considering the Passing Score boundary.

**Why this priority**: This fulfills the request to auto-generate the textual grading evaluation for the students' results without manual admin intervention.

**Independent Test**: Can be tested by submitting a dummy exam result for a student and verifying that the correct Evaluation string (e.g., "ممتاز") is assigned to their result record based on their percentage.

**Acceptance Scenarios**:

1. **Given** a student scores 90 out of 100, **When** the result is processed, **Then** the evaluation assigned is "ممتاز" (Excellent).
2. **Given** a student scores below the Passing Score, **When** the result is processed, **Then** the evaluation assigned is "ضعيف" (Fail).
3. **Given** a calculation occurs, **Then** the system uses the following standard grading scale:
   - **ممتاز (Excellent)**: 85% - 100%
   - **جيد جداً (Very Good)**: 75% - 84.9%
   - **جيد (Good)**: 65% - 74.9%
   - **مقبول (Pass)**: From (Passing Score Percentage) to 64.9%
   - **ضعيف (Fail)**: Below (Passing Score Percentage)

---

### Edge Cases

- What happens if the sum of individual question points inside an exam does not equal the explicitly set "Total Score"?
  - *Assumption*: The system scales the student's earned points proportionally to match the explicitly set "Total Score". (e.g., student gets 10/10 on questions = 100%, so 100% of Total Score (50) = 50 result).
- What happens if the `PassingScore %` is set very high by the admin (e.g., 80%)?
  - The scale adapts: "Pass" logic ensures anything from 80% to 84.9% is "Pass", overriding the "Good" or "Very Good" bands if they fall beneath the passing threshold logically.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST require `TotalScore` and `PassingScore` inputs when creating or modifying any Homework entity.
- **FR-002**: System MUST require `TotalScore` and `PassingScore` inputs when creating or modifying any Exam entity.
- **FR-003**: System MUST prevent setting a `PassingScore` that is greater than the `TotalScore`.
- **FR-004**: System MUST automatically calculate an `Evaluation` string (ممتاز, جيد جداً, جيد, مقبول, ضعيف) for every submitted student assessment (Homework or Exam).
- **FR-005**: System MUST scale the student's raw score from the questions internally if the sum of question points does not match the assessment's defined `TotalScore`.
- **FR-006**: System MUST resolve the `404 Not Found` error reported during Inline Exam Creation (`POST /api/admin/exams/inline`) by ensuring routing is active, MediatR handlers are correctly mapped, and returns `200 OK` rather than throwing internal routing exceptions.

### Key Entities

- **Exam / Homework**: Must guarantee `TotalScore` and `PassingScore` attributes exist.
- **StudentExamResult / StudentHomeworkResult**: Must store the calculated `EarnedScore` and the `Evaluation` string.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins correctly set Total and Passing scores on 100% of newly created assessments.
- **SC-002**: 100% of student submission results automatically display the correct Arabic grading evaluation (التقدير).
- **SC-003**: Zero grading calculation errors occur during score scaling (e.g. no scores exceeding 100% of Total Score due to rounding errors).
