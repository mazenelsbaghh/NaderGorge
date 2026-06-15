from tests.conftest import NaderGorgeClient, grant_package_to_student


def attach_homework(admin, lesson_id, title, question_text, question_type="Essay"):
    res = admin.post(f"/api/admin/content/lessons/{lesson_id}/homework", json={
        "title": title,
        "instructions": "Answer every question before submitting.",
        "isMandatory": True,
        "isRandomized": False,
        "requiredPointsToPass": 6,
        "totalScore": 10,
        "questions": [{
            "text": question_text,
            "order": 1,
            "points": 10,
            "type": question_type,
            "questionType": question_type,
            "options": None,
        }],
    })
    assert res.status_code == 200, f"Attach homework failed: {res.text}"
    homework_id = res.json().get("data")
    assert homework_id
    return homework_id


def get_lesson_homework(student, lesson_id):
    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code == 200, f"Lesson detail failed: {lesson_res.text}"
    homework = lesson_res.json().get("data", {}).get("homework")
    assert homework
    questions = homework.get("questions", [])
    assert len(questions) == 1
    return homework, questions[0]


def test_admin_can_create_and_replace_lesson_homework_questions(mock_package):
    lesson_id = mock_package.get("lessonId")
    package_id = mock_package.get("packageId")
    assert lesson_id
    assert package_id

    admin = NaderGorgeClient()
    admin.login_as("20000000000", app_surface="admin")

    homework_id = attach_homework(
        admin,
        lesson_id,
        "E2E Replacement Homework",
        "First homework question should be replaced",
    )
    updated_homework_id = attach_homework(
        admin,
        lesson_id,
        "E2E Replacement Homework Updated",
        "Only this updated homework question should remain",
    )
    assert updated_homework_id == homework_id

    student = NaderGorgeClient()
    user = student.login_as("20000000001")
    grant_package_to_student(package_id, user.get("id"))

    homework, question = get_lesson_homework(student, lesson_id)
    assert homework.get("id") == homework_id
    assert homework.get("title") == "E2E Replacement Homework Updated"
    assert question.get("text") == "Only this updated homework question should remain"
    assert question.get("maxPoints") == 10


def test_student_cannot_submit_homework_before_purchase_then_can_after_purchase(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")
    homework_id = mock_package.get("homeworkId")
    assert package_id
    assert lesson_id
    assert homework_id

    student = NaderGorgeClient()
    user = student.login_as("20000000001")

    blocked_res = student.post(f"/api/homework/{homework_id}/submit", json=[{
        "questionId": "00000000-0000-0000-0000-000000000000",
        "providedAnswer": "Trying to submit without buying the package.",
    }])
    assert blocked_res.status_code == 400
    assert "access" in blocked_res.text.lower()

    grant_package_to_student(package_id, user.get("id"))
    homework, question = get_lesson_homework(student, lesson_id)
    assert homework.get("id") == homework_id

    submit_res = student.post(f"/api/homework/{homework_id}/submit", json=[{
        "questionId": question.get("id"),
        "providedAnswer": "This is a valid E2E homework answer after purchase.",
    }])
    assert submit_res.status_code == 200, submit_res.text


def test_student_cannot_submit_same_homework_twice(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")
    homework_id = mock_package.get("homeworkId")
    assert package_id
    assert lesson_id
    assert homework_id

    student = NaderGorgeClient()
    user = student.login_as("20000000001")
    grant_package_to_student(package_id, user.get("id"))

    _homework, question = get_lesson_homework(student, lesson_id)
    answer = [{
        "questionId": question.get("id"),
        "providedAnswer": "First submitted homework answer.",
    }]

    first_res = student.post(f"/api/homework/{homework_id}/submit", json=answer)
    assert first_res.status_code == 200, first_res.text

    second_res = student.post(f"/api/homework/{homework_id}/submit", json=answer)
    assert second_res.status_code == 400
    assert "already submitted" in second_res.text.lower()
