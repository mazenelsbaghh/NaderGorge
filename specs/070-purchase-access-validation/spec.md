# Feature Specification: Purchase Access Validation E2E Tests

## Overview
This specification details the testing requirements for verifying access control rules related to package purchases. It ensures that students can only access educational content (lessons, videos, exams) if they have a valid access grant, either obtained through direct package activation code or by purchasing the package using their wallet balance.

## Test Scenarios

### Scenario 1: Unpurchased Access Blocked
- **Step 1**: Log in as a student device.
- **Step 2**: Attempt to load details of a lesson (`GET /api/content/lessons/{lessonId}`) that belongs to a package the student hasn't purchased.
  - **Expected**: HTTP 403 or 400 failure with message containing "access".
- **Step 3**: Attempt to start an exam (`POST /api/exams/{examId}/start`) associated with that unpurchased lesson.
  - **Expected**: HTTP 403 or 400 failure with message containing "access".

### Scenario 2: Purchase via Course Activation Code (Direct Purchase)
- **Step 1**: Admin generates an access code of type `"Package"` for a target package.
- **Step 2**: Log in as a new student.
- **Step 3**: Activate the package code using `POST /api/codes/activate`.
  - **Expected**: HTTP 200 Success.
- **Step 4**: Attempt to load details of the lesson (`GET /api/content/lessons/{lessonId}`).
  - **Expected**: HTTP 200 Success.
- **Step 5**: Attempt to start the exam (`POST /api/exams/{examId}/start`).
  - **Expected**: HTTP 200 Success.

### Scenario 3: Purchase via Balance Wallet (Wallet Purchase)
- **Step 1**: Admin generates an access code of type `"Balance"` with a sufficient amount (e.g., 150 EGP).
- **Step 2**: Log in as a new student.
- **Step 3**: Verify initial balance is 0 (`GET /api/student/balance`).
- **Step 4**: Activate the balance code (`POST /api/codes/activate`).
  - **Expected**: HTTP 200 Success, and the balance becomes 150 EGP.
- **Step 5**: Buy the package using the wallet balance (`POST /api/student/balance/purchase`).
  - **Expected**: HTTP 200 Success, and the balance is deducted by the package price.
- **Step 6**: Attempt to load details of the lesson (`GET /api/content/lessons/{lessonId}`).
  - **Expected**: HTTP 200 Success.
- **Step 7**: Attempt to start the exam (`POST /api/exams/{examId}/start`).
  - **Expected**: HTTP 200 Success.
