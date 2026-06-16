import pytest
from tests.conftest import NaderGorgeClient


def test_video_exam_locking_and_lesson_cascade(clean_db):
    # -------------------------------------------------------------
    # 1. Login as Admin
    # -------------------------------------------------------------
    admin = NaderGorgeClient()
    admin_login = admin.login("20000000000", "password")
    assert admin_login.status_code == 200, f"Admin login failed: {admin_login.text}"

    # -------------------------------------------------------------
    # 2. Create Subject
    # -------------------------------------------------------------
    subject_res = admin.post("/api/admin/subjects", json={
        "name": "E2E Physics",
        "description": "Physics Course for E2E Video Exam Locking Test"
    })
    assert subject_res.status_code == 201
    subject_id = subject_res.json().get("data")

    # -------------------------------------------------------------
    # 3. Create Teacher User & Profile
    # -------------------------------------------------------------
    teacher_user_res = admin.post("/api/admin/users", json={
        "fullName": "E2E Physics Teacher",
        "phoneNumber": "20000000099",
        "password": "password",
        "role": "Teacher"
    })
    assert teacher_user_res.status_code == 201
    teacher_user_id = teacher_user_res.json().get("data", {}).get("id")

    teacher_profile_res = admin.post("/api/admin/teachers", json={
        "userId": teacher_user_id,
        "bio": "Experienced Physics Teacher",
        "specialization": "FirstSecondary",
        "commissionRate": 0.20,
        "contactInfo": "physics_teacher@test.com",
        "subjectIds": [subject_id]
    })
    assert teacher_profile_res.status_code == 201
    teacher_profile_id = teacher_profile_res.json().get("data")

    # -------------------------------------------------------------
    # 4. Create Package
    # -------------------------------------------------------------
    package_res = admin.post("/api/admin/packages", json={
        "name": "Physics Package",
        "description": "Complete Physics Course",
        "price": 500.0,
        "subjectId": subject_id,
        "targetGrade": "1st Secondary",
        "teacherId": teacher_profile_id
    })
    assert package_res.status_code == 201
    package_id = package_res.json().get("data")

    # -------------------------------------------------------------
    # 5. Create Term (Month 1)
    # -------------------------------------------------------------
    term_res = admin.post("/api/admin/terms", json={
        "title": "Physics Month 1",
        "order": 1,
        "packageId": package_id,
        "price": 0.0
    })
    assert term_res.status_code == 201
    term_id = term_res.json().get("data")

    # -------------------------------------------------------------
    # 6. Create Content Section
    # -------------------------------------------------------------
    section_res = admin.post("/api/admin/sections", json={
        "title": "Physics Section 1",
        "order": 1,
        "termId": term_id,
        "price": 0.0
    })
    assert section_res.status_code == 201
    section_id = section_res.json().get("data")

    # -------------------------------------------------------------
    # 7. Create Lesson 1 and Lesson 2 (both free)
    # -------------------------------------------------------------
    lesson1_res = admin.post("/api/admin/lessons", json={
        "title": "Physics Lesson 1",
        "summary": "Intro to atoms",
        "order": 1,
        "sectionId": section_id,
        "price": 0.0
    })
    assert lesson1_res.status_code == 201
    lesson1_id = lesson1_res.json().get("data")

    lesson2_res = admin.post("/api/admin/lessons", json={
        "title": "Physics Lesson 2",
        "summary": "Periodic Table",
        "order": 2,
        "sectionId": section_id,
        "price": 0.0
    })
    assert lesson2_res.status_code == 201
    lesson2_id = lesson2_res.json().get("data")

    # -------------------------------------------------------------
    # 8. Create Video 1 for Lesson 1
    # -------------------------------------------------------------
    video1_res = admin.post("/api/admin/videos", json={
        "title": "Atoms Video 1",
        "provider": "youtube",
        "urlOrEmbedCode": "dQw4w9WgXcQ",
        "order": 1,
        "limit": 3,
        "lessonId": lesson1_id,
        "isActive": True
    })
    assert video1_res.status_code == 201
    video1_id = video1_res.json().get("data")

    # -------------------------------------------------------------
    # 9. Create video-level mandatory Exam for Video 1
    # -------------------------------------------------------------
    exam_res = admin.post("/api/admin/exams/inline", json={
        "title": "Atoms Video Exam",
        "description": "Solve to unlock video playback",
        "passingScore": 5.0,
        "totalScore": 10.0,
        "durationMinutes": 10,
        "isMandatory": True,
        "isRandomized": False,
        "target": {
            "type": "Video",
            "id": video1_id
        },
        "questions": [
            {
                "text": "Atom question?",
                "type": "MCQ",
                "points": 10.0,
                "order": 1,
                "options": [
                    {"text": "2", "isCorrect": True},
                    {"text": "3", "isCorrect": False}
                ]
            }
        ]
    })
    assert exam_res.status_code == 200, f"Exam creation failed: {exam_res.text}"
    exam_id = exam_res.json().get("data")

    # -------------------------------------------------------------
    # 10. Login as Student and activate Term
    # -------------------------------------------------------------
    student = NaderGorgeClient()
    student_user = student.login_as("20000000001")
    student_user_id = student_user.get("id")

    purchase_res = student.post("/api/student/balance/purchase", json={
        "contentType": "Term",
        "contentId": term_id
    })
    assert purchase_res.status_code == 200

    # -------------------------------------------------------------
    # 11. Verify Lesson 1 Video 1 is locked by its own exam
    # -------------------------------------------------------------
    l1_res = student.get(f"/api/content/lessons/{lesson1_id}")
    assert l1_res.status_code == 200
    l1_videos = l1_res.json().get("data", {}).get("videos", [])
    assert len(l1_videos) == 1
    v1_dto = l1_videos[0]
    assert v1_dto.get("isExamLocked") is True
    assert v1_dto.get("examId") == exam_id
    assert v1_dto.get("examPassed") is False

    # -------------------------------------------------------------
    # 12. Verify requesting a video playback session for Video 1 is blocked
    # -------------------------------------------------------------
    session_res = student.post("/api/student/video-session", json={
        "lessonVideoId": video1_id
    })
    assert session_res.status_code == 400 or not session_res.json().get("success")

    # -------------------------------------------------------------
    # 13. Verify Lesson 2 is locked by Lesson 1's video exam
    # -------------------------------------------------------------
    l2_res = student.get(f"/api/content/lessons/{lesson2_id}")
    assert l2_res.status_code == 200
    l2_data = l2_res.json().get("data", {})
    assert l2_data.get("isLocked") is True
    assert l2_data.get("blockingExamId") == exam_id

    # -------------------------------------------------------------
    # 14. Complete Lesson 1's Video Exam
    # -------------------------------------------------------------
    start_exam_res = student.post(f"/api/exams/{exam_id}/start")
    assert start_exam_res.status_code == 200, f"Failed to start exam: {start_exam_res.text}"
    start_data = start_exam_res.json().get("data", {})
    attempt_id = start_data.get("attemptId")
    questions = start_data.get("questions", [])
    q_id = questions[0].get("id")
    opt_id = next(opt.get("id") for opt in questions[0].get("options") if opt.get("text") == "2")

    submit_exam_res = student.post(f"/api/exams/{exam_id}/submit/{attempt_id}", json=[{
        "examQuestionId": q_id,
        "selectedOptionId": opt_id,
        "answerText": None,
        "selectedText": None,
        "audioUrl": None
    }])
    assert submit_exam_res.status_code == 200, f"Failed to submit exam: {submit_exam_res.text}"
    assert submit_exam_res.json().get("data", {}).get("isPassed") is True

    # -------------------------------------------------------------
    # 15. Verify Lesson 1 Video 1 is now unlocked
    # -------------------------------------------------------------
    l1_res_unlocked = student.get(f"/api/content/lessons/{lesson1_id}")
    assert l1_res_unlocked.status_code == 200
    v1_dto_unlocked = l1_res_unlocked.json().get("data", {}).get("videos", [])[0]
    assert v1_dto_unlocked.get("isExamLocked") is False
    assert v1_dto_unlocked.get("examPassed") is True

    # -------------------------------------------------------------
    # 16. Verify requesting a video session now succeeds
    # -------------------------------------------------------------
    session_res_ok = student.post("/api/student/video-session", json={
        "lessonVideoId": video1_id
    })
    assert session_res_ok.status_code == 200

    # -------------------------------------------------------------
    # 17. Verify Lesson 2 is now unlocked
    # -------------------------------------------------------------
    l2_res_unlocked = student.get(f"/api/content/lessons/{lesson2_id}")
    assert l2_res_unlocked.status_code == 200
    assert l2_res_unlocked.json().get("data", {}).get("isLocked") is False
