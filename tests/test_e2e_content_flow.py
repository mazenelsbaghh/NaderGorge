import pytest
from tests.conftest import NaderGorgeClient


def test_comprehensive_e2e_course_creation_and_student_access(clean_db):
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
        "description": "Physics Course for E2E Flow Test"
    })
    assert subject_res.status_code == 201, f"Subject creation failed: {subject_res.text}"
    subject_id = subject_res.json().get("data")
    assert subject_id is not None

    # -------------------------------------------------------------
    # 3. Create Teacher User & Profile
    # -------------------------------------------------------------
    teacher_user_res = admin.post("/api/admin/users", json={
        "fullName": "E2E Physics Teacher",
        "phoneNumber": "20000000088",
        "password": "password",
        "role": "Teacher"
    })
    assert teacher_user_res.status_code == 201, f"Teacher user creation failed: {teacher_user_res.text}"
    teacher_user_id = teacher_user_res.json().get("data", {}).get("id")
    assert teacher_user_id is not None

    teacher_profile_res = admin.post("/api/admin/teachers", json={
        "userId": teacher_user_id,
        "bio": "Experienced Physics Teacher",
        "specialization": "FirstSecondary",
        "commissionRate": 0.20,
        "contactInfo": "physics_teacher@test.com",
        "subjectIds": [subject_id]
    })
    assert teacher_profile_res.status_code == 201, f"Teacher profile creation failed: {teacher_profile_res.text}"
    teacher_profile_id = teacher_profile_res.json().get("data")
    assert teacher_profile_id is not None

    # -------------------------------------------------------------
    # 4. Create Yearly Package for this teacher
    # -------------------------------------------------------------
    package_res = admin.post("/api/admin/packages", json={
        "name": "Yearly Physics Package",
        "description": "Complete Physics Course for 1st Secondary",
        "price": 1000.0,
        "subjectId": subject_id,
        "targetGrade": "1st Secondary",
        "teacherId": teacher_profile_id
    })
    assert package_res.status_code == 201, f"Package creation failed: {package_res.text}"
    package_id = package_res.json().get("data")
    assert package_id is not None

    # -------------------------------------------------------------
    # 5. Create Month 1 Course (Term 1) & Month 2 Course (Term 2)
    # -------------------------------------------------------------
    term1_res = admin.post("/api/admin/terms", json={
        "title": "Month 1 Course",
        "order": 1,
        "packageId": package_id,
        "price": 200.0
    })
    assert term1_res.status_code == 201, f"Term 1 creation failed: {term1_res.text}"
    term1_id = term1_res.json().get("data")
    assert term1_id is not None

    term2_res = admin.post("/api/admin/terms", json={
        "title": "Month 2 Course",
        "order": 2,
        "packageId": package_id,
        "price": 200.0
    })
    assert term2_res.status_code == 201, f"Term 2 creation failed: {term2_res.text}"
    term2_id = term2_res.json().get("data")
    assert term2_id is not None

    # -------------------------------------------------------------
    # 6. Create Content Sections inside Month 1 and Month 2
    # -------------------------------------------------------------
    section1_res = admin.post("/api/admin/sections", json={
        "title": "Month 1 Content Section",
        "order": 1,
        "termId": term1_id,
        "price": 0.0
    })
    assert section1_res.status_code == 201, f"Section 1 creation failed: {section1_res.text}"
    section1_id = section1_res.json().get("data")
    assert section1_id is not None

    section2_res = admin.post("/api/admin/sections", json={
        "title": "Month 2 Content Section",
        "order": 1,
        "termId": term2_id,
        "price": 0.0
    })
    assert section2_res.status_code == 201, f"Section 2 creation failed: {section2_res.text}"
    section2_id = section2_res.json().get("data")
    assert section2_id is not None

    # -------------------------------------------------------------
    # 7. Create Lessons (Free & Paid) inside Month 1 & Month 2
    # -------------------------------------------------------------
    # Month 1 lessons
    m1_free_lesson_res = admin.post("/api/admin/lessons", json={
        "title": "Month 1 Free Lesson",
        "summary": "Introduction to Physics",
        "order": 1,
        "sectionId": section1_id,
        "price": 0.0,
        "examId": None
    })
    assert m1_free_lesson_res.status_code == 201
    m1_free_lesson_id = m1_free_lesson_res.json().get("data")

    m1_paid_lesson_res = admin.post("/api/admin/lessons", json={
        "title": "Month 1 Paid Lesson",
        "summary": "Deep dive into kinematics",
        "order": 2,
        "sectionId": section1_id,
        "price": 50.0,
        "examId": None
    })
    assert m1_paid_lesson_res.status_code == 201
    m1_paid_lesson_id = m1_paid_lesson_res.json().get("data")

    # Month 2 lessons
    m2_free_lesson_res = admin.post("/api/admin/lessons", json={
        "title": "Month 2 Free Lesson",
        "summary": "Intro to Dynamics",
        "order": 1,
        "sectionId": section2_id,
        "price": 0.0,
        "examId": None
    })
    assert m2_free_lesson_res.status_code == 201
    m2_free_lesson_id = m2_free_lesson_res.json().get("data")

    m2_paid_lesson_res = admin.post("/api/admin/lessons", json={
        "title": "Month 2 Paid Lesson",
        "summary": "Deep dive into Newton's Laws",
        "order": 2,
        "sectionId": section2_id,
        "price": 50.0,
        "examId": None
    })
    assert m2_paid_lesson_res.status_code == 201
    m2_paid_lesson_id = m2_paid_lesson_res.json().get("data")

    # -------------------------------------------------------------
    # 8. Add Videos, inline Exams, and Homework to all 4 lessons
    # -------------------------------------------------------------
    lesson_ids = [m1_free_lesson_id, m1_paid_lesson_id, m2_free_lesson_id, m2_paid_lesson_id]

    for index, lesson_id in enumerate(lesson_ids):
        # Create Video
        video_res = admin.post("/api/admin/videos", json={
            "title": f"Video for Lesson {index + 1}",
            "provider": "youtube",
            "urlOrEmbedCode": "dQw4w9WgXcQ",
            "order": 1,
            "limit": 3,
            "lessonId": lesson_id,
            "isActive": True
        })
        assert video_res.status_code == 201, f"Video creation failed: {video_res.text}"

        # Create inline Exam targeting this lesson
        exam_res = admin.post("/api/admin/exams/inline", json={
            "title": f"Exam for Lesson {index + 1}",
            "description": "Answer to pass",
            "passingScore": 5.0,
            "totalScore": 10.0,
            "durationMinutes": 15,
            "isMandatory": True,
            "isRandomized": False,
            "target": {
                "type": "Lesson",
                "id": lesson_id
            },
            "questions": [
                {
                    "text": "1+1=?",
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

        # Attach Homework
        hw_res = admin.post(f"/api/admin/content/lessons/{lesson_id}/homework", json={
            "title": f"Homework for Lesson {index + 1}",
            "instructions": "Solve all questions.",
            "isMandatory": True,
            "isRandomized": False,
            "requiredPointsToPass": 6,
            "totalScore": 10,
            "questions": [
                {
                    "text": "1+1=?",
                    "order": 1,
                    "points": 10,
                    "type": "MCQ",
                    "options": [
                        {"text": "2", "isCorrect": True},
                        {"text": "3", "isCorrect": False}
                    ]
                }
            ]
        })
        assert hw_res.status_code == 200, f"Homework attachment failed: {hw_res.text}"

    # -------------------------------------------------------------
    # 9. Student access checks (Before purchase)
    # -------------------------------------------------------------
    student = NaderGorgeClient()
    student_user = student.login_as("20000000001")
    student_user_id = student_user.get("id")
    assert student_user_id is not None

    # Verify the package list endpoint returns isEnrolled = False initially
    pk_list_initial = student.get("/api/content/packages")
    assert pk_list_initial.status_code == 200
    pk_list_data = pk_list_initial.json().get("data", [])
    pkg_dto_initial = next((p for p in pk_list_data if p.get("id") == package_id), None)
    assert pkg_dto_initial is not None
    assert pkg_dto_initial.get("isEnrolled") is False

    # Verify Paid Lessons are blocked (Minimal detail returned with hasAccess = false)
    for paid_lesson_id in [m1_paid_lesson_id, m2_paid_lesson_id]:
        detail_res = student.get(f"/api/content/lessons/{paid_lesson_id}")
        assert detail_res.status_code == 200
        detail_data = detail_res.json().get("data", {})
        assert detail_data.get("hasAccess") is False
        assert len(detail_data.get("videos", [])) == 0
        assert detail_data.get("homework") is None

    # Verify Free Lessons details are blocked initially because student hasn't enrolled (Price = 0 purchase)
    for free_lesson_id in [m1_free_lesson_id, m2_free_lesson_id]:
        detail_res = student.get(f"/api/content/lessons/{free_lesson_id}")
        assert detail_res.status_code == 200
        assert detail_res.json().get("data", {}).get("hasAccess") is False

    # Enroll in Month 1 Free Lesson (Price = 0 purchase)
    purchase_free_res = student.post("/api/student/balance/purchase", json={
        "contentType": "Lesson",
        "contentId": m1_free_lesson_id
    })
    assert purchase_free_res.status_code == 200, f"Free purchase failed: {purchase_free_res.text}"

    # Verify the package list endpoint returns isEnrolled = True for this package
    # because the student has active access to a lesson inside it.
    pk_list_after = student.get("/api/content/packages")
    assert pk_list_after.status_code == 200
    pk_list_data_after = pk_list_after.json().get("data", [])
    pkg_dto_after = next((p for p in pk_list_data_after if p.get("id") == package_id), None)
    assert pkg_dto_after is not None
    assert pkg_dto_after.get("isEnrolled") is True, "Package should show as enrolled because student activated a lesson inside it"

    # Verify Month 1 Free Lesson is now unlocked and returns complete details
    unlocked_free_res = student.get(f"/api/content/lessons/{m1_free_lesson_id}")
    assert unlocked_free_res.status_code == 200
    unlocked_data = unlocked_free_res.json().get("data", {})
    assert unlocked_data.get("hasAccess") is True
    assert len(unlocked_data.get("videos", [])) > 0
    assert unlocked_data.get("homework") is not None

    # -------------------------------------------------------------
    # 10. Student wallet recharge & Package Purchase
    # -------------------------------------------------------------
    # Recharge wallet via admin balance adjust
    recharge_res = admin.post(f"/api/admin/users/students/{student_user_id}/balance/adjust", json={
        "amount": 1500.0,
        "reason": "E2E Yearly Package Purchase"
    })
    assert recharge_res.status_code == 200, f"Recharge failed: {recharge_res.text}"

    # Purchase the Yearly Package
    buy_pkg_res = student.post("/api/student/balance/purchase", json={
        "contentType": "Package",
        "contentId": package_id
    })
    assert buy_pkg_res.status_code == 200, f"Package purchase failed: {buy_pkg_res.text}"

    # Verify student balance was deducted by 1000 EGP
    bal_res = student.get("/api/student/balance")
    assert bal_res.status_code == 200
    assert bal_res.json().get("data", {}).get("currentBalance") == 500.0

    # -------------------------------------------------------------
    # 11. Student access checks (After Yearly Package purchase)
    # -------------------------------------------------------------
    # Verify both Month 1 and Month 2 paid lessons are now accessible (hasAccess = True)
    # But because they are the second lessons in their sections, they should be locked by progression lock
    for paid_lesson_id in [m1_paid_lesson_id, m2_paid_lesson_id]:
        detail_res = student.get(f"/api/content/lessons/{paid_lesson_id}")
        assert detail_res.status_code == 200
        detail_data = detail_res.json().get("data", {})
        assert detail_data.get("hasAccess") is True
        assert len(detail_data.get("videos", [])) > 0
        assert detail_data.get("homework") is not None

    # Fetch Month 1 Free Lesson (Lesson 1) details to get its exam and homework
    l1_res = student.get(f"/api/content/lessons/{m1_free_lesson_id}")
    assert l1_res.status_code == 200
    l1_data = l1_res.json().get("data", {})
    l1_exam_id = l1_data.get("examId")
    assert l1_exam_id is not None

    l1_homework = l1_data.get("homework")
    assert l1_homework is not None
    l1_homework_id = l1_homework.get("id")
    l1_homework_q_id = l1_homework.get("questions")[0].get("id")

    # Fetch Month 1 Paid Lesson (Lesson 2) details - should be locked due to Lesson 1's exam
    l2_res = student.get(f"/api/content/lessons/{m1_paid_lesson_id}")
    assert l2_res.status_code == 200
    l2_data = l2_res.json().get("data", {})
    assert l2_data.get("isLocked") is True
    assert l2_data.get("blockingExamId") == l1_exam_id
    assert l2_data.get("blockingHomeworkLessonId") is None

    # -------------------------------------------------------------
    # 12. Complete Lesson 1's Mandatory Exam
    # -------------------------------------------------------------
    # Start the exam attempt
    start_exam_res = student.post(f"/api/exams/{l1_exam_id}/start")
    assert start_exam_res.status_code == 200, f"Failed to start exam: {start_exam_res.text}"
    start_exam_data = start_exam_res.json().get("data", {})
    attempt_id = start_exam_data.get("attemptId")
    questions = start_exam_data.get("questions", [])
    assert len(questions) > 0

    # Solve the MCQ question (correct answer text is "2")
    q = questions[0]
    options = q.get("options", [])
    selected_option = next(opt for opt in options if opt.get("text") == "2")
    selected_option_id = selected_option.get("id")

    answers = [
        {
            "examQuestionId": q.get("id"),
            "selectedOptionId": selected_option_id,
            "answerText": None,
            "selectedText": None,
            "audioUrl": None
        }
    ]

    # Submit the exam
    submit_exam_res = student.post(f"/api/exams/{l1_exam_id}/submit/{attempt_id}", json=answers)
    assert submit_exam_res.status_code == 200, f"Failed to submit exam: {submit_exam_res.text}"
    assert submit_exam_res.json().get("data", {}).get("isPassed") is True

    # -------------------------------------------------------------
    # 13. Verify Lesson 2 is still locked by Lesson 1's Homework
    # -------------------------------------------------------------
    l2_res_after_exam = student.get(f"/api/content/lessons/{m1_paid_lesson_id}")
    assert l2_res_after_exam.status_code == 200
    l2_data_after_exam = l2_res_after_exam.json().get("data", {})
    assert l2_data_after_exam.get("isLocked") is True
    assert l2_data_after_exam.get("blockingExamId") is None
    assert l2_data_after_exam.get("blockingHomeworkLessonId") == m1_free_lesson_id

    # -------------------------------------------------------------
    # 14. Complete Lesson 1's Mandatory Homework
    # -------------------------------------------------------------
    # Start the homework attempt
    start_hw_res = student.get(f"/api/homework/{l1_homework_id}/start")
    assert start_hw_res.status_code == 200, f"Failed to start homework: {start_hw_res.text}"

    # Submit MCQ answer (correct answer is "2")
    hw_answers = [
        {
            "questionId": l1_homework_q_id,
            "providedAnswer": "2"
        }
    ]
    submit_hw_res = student.post(f"/api/homework/{l1_homework_id}/submit", json=hw_answers)
    assert submit_hw_res.status_code == 200, f"Failed to submit homework: {submit_hw_res.text}"
    assert submit_hw_res.json().get("success") is True

    # -------------------------------------------------------------
    # 15. Verify Lesson 2 is unlocked from Lesson 1 requirements
    # But it should be blocked by its own mandatory exam
    # -------------------------------------------------------------
    l2_res_unlocked = student.get(f"/api/content/lessons/{m1_paid_lesson_id}")
    assert l2_res_unlocked.status_code == 200
    l2_data_unlocked = l2_res_unlocked.json().get("data", {})
    l2_exam_id = l2_data_unlocked.get("examId")
    assert l2_exam_id is not None
    assert l2_data_unlocked.get("isLocked") is True
    assert l2_data_unlocked.get("blockingExamId") == l2_exam_id
    assert l2_data_unlocked.get("blockingHomeworkLessonId") is None

    # -------------------------------------------------------------
    # 16. Complete Lesson 2's Mandatory Exam
    # -------------------------------------------------------------
    start_l2_exam_res = student.post(f"/api/exams/{l2_exam_id}/start")
    assert start_l2_exam_res.status_code == 200, f"Failed to start exam: {start_l2_exam_res.text}"
    start_l2_exam_data = start_l2_exam_res.json().get("data", {})
    l2_attempt_id = start_l2_exam_data.get("attemptId")
    l2_questions = start_l2_exam_data.get("questions", [])
    assert len(l2_questions) > 0

    l2_q = l2_questions[0]
    l2_options = l2_q.get("options", [])
    l2_selected_option = next(opt for opt in l2_options if opt.get("text") == "2")
    l2_selected_option_id = l2_selected_option.get("id")

    l2_answers = [
        {
            "examQuestionId": l2_q.get("id"),
            "selectedOptionId": l2_selected_option_id,
            "answerText": None,
            "selectedText": None,
            "audioUrl": None
        }
    ]

    submit_l2_exam_res = student.post(f"/api/exams/{l2_exam_id}/submit/{l2_attempt_id}", json=l2_answers)
    assert submit_l2_exam_res.status_code == 200, f"Failed to submit exam: {submit_l2_exam_res.text}"
    assert submit_l2_exam_res.json().get("data", {}).get("isPassed") is True

    # -------------------------------------------------------------
    # 17. Verify Lesson 2 is now completely unlocked!
    # -------------------------------------------------------------
    l2_res_fully_unlocked = student.get(f"/api/content/lessons/{m1_paid_lesson_id}")
    assert l2_res_fully_unlocked.status_code == 200
    l2_data_fully_unlocked = l2_res_fully_unlocked.json().get("data", {})
    assert l2_data_fully_unlocked.get("isLocked") is False
    assert l2_data_fully_unlocked.get("blockingExamId") is None
    assert l2_data_fully_unlocked.get("blockingHomeworkLessonId") is None

