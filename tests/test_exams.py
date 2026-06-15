import pytest
import uuid
from tests.conftest import NaderGorgeClient, BASE_URL, INTERNAL_SECRET

def test_mcq_exam_lifecycle(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")
    exam_id = mock_package.get("examId")
    assert exam_id is not None

    # Login as Student 1
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Grant package to Student 1 so they can load exams
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")
    admin.post("/api/e2e/grant-package", json={"packageId": package_id, "userId": student.login("20000000001", "password").json().get("data", {}).get("user", {}).get("id")})

    # Step 1: Start exam attempt
    start_res = student.post(f"/api/exams/{exam_id}/start")
    assert start_res.status_code == 200


    attempt_data = start_res.json().get("data", {})
    print("ATTEMPT DATA:", attempt_data)
    attempt_id = attempt_data.get("attemptId")
    assert attempt_id is not None

    questions = attempt_data.get("questions", [])
    assert len(questions) > 0
    question = questions[0]
    question_id = question.get("id") # junction ID or item ID
    options = question.get("options", [])
    assert len(options) > 0

    # Find incorrect option ID
    incorrect_option = [o for o in options if o.get("text") == "3"][0]
    incorrect_option_id = incorrect_option.get("id")

    # Find correct option ID
    correct_option = [o for o in options if o.get("text") == "2"][0]
    correct_option_id = correct_option.get("id")

    # Step 2: Submit incorrect MCQ answer
    submit_res = student.post(f"/api/exams/{exam_id}/submit/{attempt_id}", json=[{
        "examQuestionId": question_id,
        "selectedOptionId": incorrect_option_id,
        "answerText": None
    }])
    print("SUBMIT RES STATUS:", submit_res.status_code)
    print("SUBMIT RES BODY:", submit_res.text)
    assert submit_res.status_code == 200
    assert submit_res.json().get("data", {}).get("isPassed") is False

    # Step 3: Start a new attempt to submit correct answer
    start_res2 = student.post(f"/api/exams/{exam_id}/start")
    assert start_res2.status_code == 200
    attempt_id2 = start_res2.json().get("data", {}).get("attemptId")

    submit_res2 = student.post(f"/api/exams/{exam_id}/submit/{attempt_id2}", json=[{
        "examQuestionId": question_id,
        "selectedOptionId": correct_option_id,
        "answerText": None
    }])
    assert submit_res2.status_code == 200
    assert submit_res2.json().get("data", {}).get("isPassed") is True

def test_homework_submission(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")
    homework_id = mock_package.get("homeworkId")
    assert homework_id is not None

    student = NaderGorgeClient()
    student.login("20000000001", "password")

    admin = NaderGorgeClient()
    admin.login("20000000000", "password")
    admin.post("/api/e2e/grant-package", json={"packageId": package_id, "userId": student.login("20000000001", "password").json().get("data", {}).get("user", {}).get("id")})

    # Retrieve lesson detail to find homework question ID
    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code == 200
    homework = lesson_res.json().get("data", {}).get("homework", {})
    assert homework is not None
    questions = homework.get("questions", [])
    assert len(questions) > 0
    question_id = questions[0].get("id")

    # Submit homework
    submit_res = student.post(f"/api/homework/{homework_id}/submit", json=[{
        "questionId": question_id,
        "providedAnswer": "This is an E2E test answer essay text."
    }])
    assert submit_res.status_code == 200

def test_essay_grading_callback_security():
    # Call callback with incorrect header token
    admin = NaderGorgeClient()
    dummy_submission_id = str(uuid.uuid4())

    headers_bad = {"X-Internal-Token": "invalid_secret_token"}
    res_bad = admin.post("/api/v1/internal/callbacks/essay-graded", json={
        "essaySubmissionId": dummy_submission_id,
        "aiScore": 8.0,
        "aiFeedback": "Good feedback"
    }, headers=headers_bad)
    assert res_bad.status_code == 401

    # Call callback with correct header token but dummy ID -> should not return 401
    headers_ok = {"X-Internal-Token": INTERNAL_SECRET}
    res_ok = admin.post("/api/v1/internal/callbacks/essay-graded", json={
        "essaySubmissionId": dummy_submission_id,
        "aiScore": 8.0,
        "aiFeedback": "Good feedback"
    }, headers=headers_ok)
    assert res_ok.status_code == 400 or res_ok.status_code == 404

def test_essay_exam_lifecycle(mock_package):
    package_id = mock_package.get("packageId")
    essay_exam_id = mock_package.get("essayExamId")
    assert essay_exam_id is not None

    # Login as Student 1
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Grant package to Student 1 so they can load exams
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")
    admin.post("/api/e2e/grant-package", json={"packageId": package_id, "userId": student.login("20000000001", "password").json().get("data", {}).get("user", {}).get("id")})

    # Start essay exam attempt
    start_res = student.post(f"/api/exams/{essay_exam_id}/start")
    assert start_res.status_code == 200
    attempt_data = start_res.json().get("data", {})
    attempt_id = attempt_data.get("attemptId")
    assert attempt_id is not None

    questions = attempt_data.get("questions", [])
    assert len(questions) > 0
    question = questions[0]
    question_id = question.get("id")

    # Submit essay answer
    submit_res = student.post(f"/api/exams/{essay_exam_id}/submit/{attempt_id}", json=[{
        "examQuestionId": question_id,
        "selectedOptionId": None,
        "answerText": "Gravity pulls objects together."
    }])
    assert submit_res.status_code == 200
    submit_data = submit_res.json().get("data", {})
    assert submit_data.get("isPassed") is False
    assert submit_data.get("resultState") == "Pending"

    # Admin retrieves pending essays to find our essaySubmissionId
    pending_res = admin.get("/api/admin/essays/pending")
    assert pending_res.status_code == 200
    pending_list = pending_res.json().get("data", [])
    assert len(pending_list) > 0

    essay_submission = [e for e in pending_list if e.get("answerText") == "Gravity pulls objects together."][0]
    essay_submission_id = essay_submission.get("id")
    assert essay_submission_id is not None

    # Webhook AI grading callback
    headers = {"X-Internal-Token": INTERNAL_SECRET}
    callback_res = admin.post("/api/v1/internal/callbacks/essay-graded", json={
        "essaySubmissionId": essay_submission_id,
        "aiScore": 8.0,
        "aiFeedback": "Excellent gravity explanation."
    }, headers=headers)
    assert callback_res.status_code == 200

    # Retrieve pending list again to verify status transitioned
    pending_res2 = admin.get("/api/admin/essays/pending")
    assert pending_res2.status_code == 200
    pending_list2 = pending_res2.json().get("data", [])
    essay_submission2 = [e for e in pending_list2 if e.get("id") == essay_submission_id][0]
    assert essay_submission2.get("status") in [1, 2, "AIScored", "WaitTeacher"] # AIScored or WaitTeacher

    # Admin (Teacher) final grade submission
    grade_res = admin.post(f"/api/admin/essays/{essay_submission_id}/grade", json={
        "essaySubmissionId": essay_submission_id,
        "teacherScore": 9.0,
        "teacherFeedback": "Very well written essay."
    })
    assert grade_res.status_code == 200

    # Verify student's attempt is now completed and passed!
    status_res = student.get(f"/api/exams/attempts/{attempt_id}/grading-status")
    assert status_res.status_code == 200
    assert status_res.json().get("data", {}).get("resultState") == "Completed"

    latest_res = student.get(f"/api/exams/{essay_exam_id}/latest-passed-result")
    assert latest_res.status_code == 200

def test_exam_lifelines(mock_package):
    package_id = mock_package.get("packageId")
    exam_id = mock_package.get("examId")

    # Login as Student 1
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Grant package to Student 1
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")
    admin.post("/api/e2e/grant-package", json={
        "packageId": package_id, 
        "userId": student.login("20000000001", "password").json().get("data", {}).get("user", {}).get("id")
    })

    # Start attempt
    start_res = student.post(f"/api/exams/{exam_id}/start")
    assert start_res.status_code == 200
    attempt_data = start_res.json().get("data", {})
    attempt_id = attempt_data.get("attemptId")
    assert attempt_id is not None

    questions = attempt_data.get("questions", [])
    assert len(questions) > 0
    question = questions[0]
    question_id = question.get("id")

    # 1. Use Fifty-Fifty lifeline
    ff_res = student.get(f"/api/exams/{exam_id}/attempts/{attempt_id}/questions/{question_id}/fifty-fifty")
    assert ff_res.status_code == 200
    ff_data = ff_res.json().get("data", [])
    assert len(ff_data) >= 1
    assert ff_data[0] is not None

    # 2. Use Swap Question lifeline (should fail as there are no extra questions seeded in the mock exam)
    swap_res = student.post(f"/api/exams/{exam_id}/attempts/{attempt_id}/questions/{question_id}/swap")
    assert swap_res.status_code == 400
    assert "لا يوجد أسئلة إضافية" in swap_res.text or "available" in swap_res.text.lower()


