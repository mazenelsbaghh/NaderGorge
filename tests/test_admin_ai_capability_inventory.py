import hashlib
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT / "tests" / "admin_ai_capability_baseline.json"
SCHEMA = ROOT / "tests" / "admin_ai_capability_manifest.schema.json"


def _canonical_digest(payload):
    copy = dict(payload)
    copy.pop("digest", None)
    return hashlib.sha256(
        json.dumps(copy, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
    ).hexdigest()


def test_baseline_has_closed_manifest_shape_and_deterministic_digest():
    schema = json.loads(SCHEMA.read_text())
    baseline = json.loads(BASELINE.read_text())

    assert schema["properties"]["schemaVersion"]["const"] == baseline["schemaVersion"]
    assert baseline["digest"] == _canonical_digest(baseline)
    assert baseline["activation"] in {"blocked", "reviewed", "active", "superseded"}
    assert baseline["sources"]["semantic"]["digest"] == hashlib.sha256(
        (ROOT / "scripts/generate-admin-ai-capability-baseline.mjs").read_bytes()
    ).hexdigest()


def test_baseline_has_one_disposition_per_item_without_duplicate_id_or_route_method():
    baseline = json.loads(BASELINE.read_text())
    items = baseline["items"]
    ids = [item["id"] for item in items]
    excluded_ids = [item["id"] for item in baseline.get("exclusions", [])]
    route_methods = [(item["kind"], item["method"], item["route"], item["source"]["file"], item["source"]["line"]) for item in items]

    assert len(ids) == len(set(ids))
    assert len(excluded_ids) == len(set(excluded_ids))
    assert not set(ids).intersection(excluded_ids)
    assert len(route_methods) == len(set(route_methods))
    assert all(item["status"] != "excluded" for item in items)
    assert all(item["status"] != "blocked" or item.get("blocker") for item in items)


def test_baseline_uses_only_approved_exclusion_reasons():
    baseline = json.loads(BASELINE.read_text())
    allowed = set(json.loads(SCHEMA.read_text())["$defs"]["exclusion"]["properties"]["reason"]["enum"])

    assert all(exclusion["reason"] in allowed for exclusion in baseline.get("exclusions", []))
    self_service = [item for item in baseline["exclusions"] if item["reason"] == "self-service"]
    admin_ai_transport = [item for item in self_service if "Admin AI conversation/proposal transport" in item["detail"]]
    playback = [item for item in self_service if "Current-viewer playback session" in item["detail"]]
    auth_refresh = [item for item in self_service if "Current-user authentication refresh" in item["detail"]]
    public_surface = [item for item in baseline["exclusions"] if item["reason"] == "public-surface"]
    teacher_surface = [item for item in baseline["exclusions"] if item["reason"] == "teacher-surface"]
    teacher_reports = [item for item in teacher_surface if "/teacher/reports/" in item["detail"]]
    teacher_other = [item for item in teacher_surface if "/teacher/reports/" not in item["detail"]]
    assert len(admin_ai_transport) == 13
    assert len(playback) == 9
    assert len(auth_refresh) == 1
    assert len(self_service) == len(admin_ai_transport) + len(playback) + len(auth_refresh)
    assert len(public_surface) == 4
    expected_public = {
        "GET /public/forms/{slug}", "POST /public/forms/{slug}/submit",
        "GET /public/settings",
        "GET /live-support/participant/conversations/{conversationId}/attachments/{attachmentId}",
    }
    assert {item["detail"].split(": ", 1)[1] for item in public_surface} == expected_public
    assert 'audience="staff"' in (
        ROOT / "frontend/src/components/live-support/staff/StaffConversationWorkspace.tsx"
    ).read_text()
    assert 'audience="staff"' in (
        ROOT / "frontend/src/components/live-support/admin/ConversationInvestigation.tsx"
    ).read_text()
    assert 'liveSupportService.getAttachmentBlob(' in (
        ROOT / "frontend/src/components/live-support/LiveSupportMessageContent.tsx"
    ).read_text()
    assert 'getPublicForm(slug)' in (
        ROOT / "frontend/src/app/forms/[slug]/PublicFormPageClient.tsx"
    ).read_text()
    assert 'submitPublicForm(slug, answers)' in (
        ROOT / "frontend/src/app/forms/[slug]/PublicFormPageClient.tsx"
    ).read_text()
    playback_calls = [call for call in json.loads(
        (ROOT / "tests/admin_ai_frontend_reachable_calls.json").read_text())["calls"]
        if call["source"]["file"] == "frontend/src/services/video-session-service.ts"
        and (call["path"] == "/api/video/session" or call["path"].startswith("/student/video-session"))]
    assert len(playback_calls) == len(playback)
    assert all(any(item["detail"].endswith(f'{call["method"]} {call["path"]}')
                   for item in playback) for call in playback_calls)
    assert not any(item["kind"] == "frontend-call"
                   and item["source"]["file"] == "frontend/src/services/video-session-service.ts"
                   for item in baseline["items"])
    video_auth = (ROOT / "backend/src/NaderGorge.API/Authorization/VideoPlaybackAuthorization.cs").read_text()
    video_controller = (ROOT / "backend/src/NaderGorge.API/Controllers/VideoSessionController.cs").read_text()
    assert 'user.IsInRole("Admin")' in video_auth
    assert 'VideoSessionMode.AdminPreview' in video_controller
    assert 'session.UserId == userId' in video_controller
    assert len(teacher_reports) == 9
    assert len(teacher_other) == 7
    assert len(baseline["exclusions"]) == len(self_service) + len(teacher_surface) + len(public_surface)
    assert '[Authorize(Roles = "Teacher")]' in (
        ROOT / "backend/src/NaderGorge.API/Controllers/TeacherReportsController.cs"
    ).read_text()
    calls = json.loads((ROOT / "tests/admin_ai_frontend_reachable_calls.json").read_text())["calls"]
    teacher_calls = [call for call in calls
                     if call["source"]["file"] == "frontend/src/services/advanced-report-service.ts"
                     and call["path"].startswith("/teacher/reports/")]
    assert len(teacher_calls) == len(teacher_reports)
    for call in teacher_calls:
        assert any(item["detail"].endswith(f'{call["method"]} {call["path"]}')
                   for item in teacher_reports)
        assert any(item["kind"] == "frontend-call" and item["method"] == call["method"]
                   and item["route"] == call["path"].replace("/teacher/reports/", "/admin/reports/", 1)
                   for item in baseline["items"])
        if call["method"] == "DELETE":
            admin_delete = [item for item in baseline["items"]
                            if item["kind"] == "frontend-call" and item["method"] == "DELETE"
                            and item["route"] == call["path"].replace(
                                "/teacher/reports/", "/admin/reports/", 1)]
            assert len(admin_delete) == 1
            assert admin_delete[0]["risk"] == admin_delete[0]["confirmation"] == "strong"
    assert not any(item["kind"] == "frontend-call" and item["route"].startswith("/teacher/reports/")
                   for item in baseline["items"])
    expected_teacher_only = {
        "/teacher/codes/groups": "frontend/src/services/admin-service.ts",
        "/teacher/codes/groups/{id}/details": "frontend/src/services/admin-service.ts",
        "/teacher/context": "frontend/src/services/teacher-service.ts",
        "/teacher/content/{contentType}/{id}/subscribers": "frontend/src/services/teacher-service.ts",
        "/teacher/content/{contentType}/{id}/subscribers/export": "frontend/src/services/teacher-service.ts",
        "/teacher/finance/statement": "frontend/src/services/finance-service.ts",
        "/teacher/finance/statement/pdf": "frontend/src/services/finance-service.ts",
    }
    for controller in ("TeacherController.cs", "TeacherFinanceController.cs"):
        assert '[Authorize(Roles = "Teacher")]' in (
            ROOT / "backend/src/NaderGorge.API/Controllers" / controller
        ).read_text()
    for route, source_file in expected_teacher_only.items():
        assert any(call["method"] == "GET" and call["path"] == route
                   and call["source"]["file"] == source_file for call in calls)
        assert any(item["detail"].endswith(f"GET {route}") for item in teacher_other)
        assert not any(item["kind"] == "frontend-call" and item["route"] == route
                       for item in baseline["items"])
    admin_controller = (ROOT / "backend/src/NaderGorge.API/Controllers/AdminController.cs").read_text()
    admin_finance = (ROOT / "backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs").read_text()
    assert '[HttpGet("codes/groups")]' in admin_controller
    assert '[HttpGet("codes/groups/{id:guid}/details")]' in admin_controller
    assert '[HttpGet("teachers/{teacherId:guid}/statement")]' in admin_finance
    assert '[HttpGet("teachers/{teacherId:guid}/statement/pdf")]' in admin_finance


def test_frontend_calls_with_exact_backend_routes_share_the_authoritative_operation():
    items = json.loads(BASELINE.read_text())["items"]

    def route_key(item):
        route = re.sub(r"\{[^}]+\}", "{}", item["route"].split("?", 1)[0].lower())
        return item["method"], re.sub(r"^/api(?=/)", "", route)

    backend = {route_key(item): item for item in items if item["kind"] == "backend-endpoint"}
    matched = [item for item in items if item["kind"] == "frontend-call" and route_key(item) in backend]

    assert matched
    assert all(item["authoritativeOperation"] == backend[route_key(item)]["authoritativeOperation"]
               for item in matched)
    for item in matched:
        source = backend[route_key(item)]
        for field in ("effect", "domain", "risk", "confirmation", "status", "limits",
                      "idempotency", "concurrency", "audit", "refreshScopes", "blocker"):
            assert item.get(field) == source.get(field), (item["route"], field)


def test_admin_accessible_shared_task_routes_map_to_original_commands():
    items = json.loads(BASELINE.read_text())["items"]
    expected = {
        "/api/v1/assistant/tasks/my/{id}/comments": "AddTaskCommentCommand",
        "/api/v1/assistant/tasks/my/{id}/status": "UpdateTaskStatusCommand",
    }
    controller = (ROOT / "backend/src/NaderGorge.API/Controllers/AssistantController.cs").read_text()
    for route, command in expected.items():
        backend = [item for item in items if item["kind"] == "backend-endpoint"
                   and item["method"] == "POST" and item["route"] == route]
        assert len(backend) == 1
        assert backend[0]["authoritativeOperation"] == f"command:{command}"
        assert backend[0]["status"] == "blocked"
        assert f"new {command}(" in controller
        frontend = [item for item in items if item["kind"] == "frontend-call"
                    and item["method"] == "POST"
                    and re.sub(r"\{[^}]+\}", "{}", item["route"].lower()) ==
                    re.sub(r"\{[^}]+\}", "{}", route.removeprefix("/api").lower())]
        assert len(frontend) == 1
        assert frontend[0]["authoritativeOperation"] == f"command:{command}"


def test_admin_page_content_reads_link_to_authenticated_backend_routes():
    items = json.loads(BASELINE.read_text())["items"]
    expected = {
        "/api/content/packages": "ContentController.GetPackages",
        "/api/content/packages/{packageid}/terms": "ContentController.GetTerms",
        "/api/content/terms/{termid}/sections": "ContentController.GetSections",
        "/api/content/sections/{sectionid}/lessons": "ContentController.GetLessons",
        "/api/content/lessons/{lessonid}": "ContentController.GetLessonDetail",
        "/api/video-learning/{videoid}": "VideoLearningController.Read",
    }
    for route, operation in expected.items():
        backend = [item for item in items if item["kind"] == "backend-endpoint"
                   and item["method"] == "GET" and item["route"].lower() == route]
        assert len(backend) == 1
        assert backend[0]["authoritativeOperation"] == f"diagnostic:{operation}"
        assert backend[0]["status"] == "candidate"
        normalized = re.sub(r"\{[^}]+\}", "{}", route.removeprefix("/api"))
        frontend = [item for item in items if item["kind"] == "frontend-call"
                    and item["method"] == "GET"
                    and re.sub(r"\{[^}]+\}", "{}", item["route"].lower()) == normalized]
        assert frontend
        assert all(item["authoritativeOperation"] == backend[0]["authoritativeOperation"]
                   for item in frontend)
    assert '[Authorize]' in (
        ROOT / "backend/src/NaderGorge.API/Controllers/ContentController.cs"
    ).read_text()


def test_admin_question_audio_upload_remains_a_blocked_storage_effect():
    items = json.loads(BASELINE.read_text())["items"]
    backend = [item for item in items if item["kind"] == "backend-endpoint"
               and item["method"] == "POST" and item["route"] == "/api/student/upload-audio"]
    frontend = [item for item in items if item["kind"] == "frontend-call"
                and item["method"] == "POST" and item["route"] == "/student/upload-audio"]
    assert len(backend) == len(frontend) == 1
    assert backend[0]["authoritativeOperation"] == frontend[0]["authoritativeOperation"]
    assert backend[0]["status"] == frontend[0]["status"] == "blocked"
    assert backend[0]["effect"] == "external-side-effect"
    assert 'studentService.uploadAudio(file)' in (
        ROOT / "frontend/src/components/admin/QuestionEditor.tsx"
    ).read_text()
    assert 'SharedFileArea.Public' in (
        ROOT / "backend/src/NaderGorge.API/Controllers/StudentController.cs"
    ).read_text()


def test_admin_accessible_learning_center_is_covered_despite_dynamic_frontend_builder():
    items = json.loads(BASELINE.read_text())["items"]
    endpoints = json.loads((ROOT / "tests/endpoint_inventory.json").read_text())["endpoints"]
    expected = {(endpoint["method"], endpoint["path"])
                for endpoint in endpoints if endpoint["controller"] == "LearningCenterController"}
    actual = {(item["method"], item["route"]) for item in items
              if item["kind"] == "backend-endpoint" and item["route"].startswith("/api/learning-center/")}
    assert expected == actual
    assert any(item["route"] == "/{base}/{path}" and item["status"] == "candidate"
               for item in items if item["kind"] == "frontend-call")
    assert '[Authorize(Roles = "Admin,Teacher")]' in (
        ROOT / "backend/src/NaderGorge.API/Controllers/LearningCenterController.cs"
    ).read_text()


def test_reviewed_read_only_posts_do_not_inherit_mutation_requirements():
    items = json.loads(BASELINE.read_text())["items"]
    expected = {
        "diagnostic:AssessmentReviewController.PreviewExamRevision": "preview",
        "diagnostic:AssessmentReviewController.PreviewHomeworkRevision": "preview",
        "diagnostic:AdminTeacherFinanceCenterController.PreviewSettlement": "preview",
        "diagnostic:AdminTeacherFinanceCenterController.PreviewSharedPackageAllocation": "preview",
        "diagnostic:HrShiftsController.ValidateAssignments": "read",
        "diagnostic:WhatsAppCampaignController.Preview": "preview",
        "diagnostic:WhatsAppCampaignController.InspectSpreadsheet": "read",
        "diagnostic:WhatsAppCampaignController.ContactCandidates": "read",
    }
    backend = {item["authoritativeOperation"]: item for item in items
               if item["kind"] == "backend-endpoint" and item["method"] == "POST"}

    assert set(expected).issubset(backend)
    for operation, effect in expected.items():
        item = backend[operation]
        assert (item["effect"], item["risk"], item["status"]) == (effect, "none", "candidate")
        assert item["idempotency"] == item["concurrency"] == "none"
        assert item["audit"] == "read-evidence"
        assert "blocker" not in item

    # A similar name can still call an external service or mutate state.
    assert backend["diagnostic:LiveSupportAIAdminController.Preview"]["status"] == "blocked"
    assert backend["diagnostic:AdminFacebookMessengerController.CheckPage"]["status"] == "blocked"


def test_unresolved_frontend_deletes_require_strong_confirmation():
    items = json.loads(BASELINE.read_text())["items"]
    deletes = [item for item in items if item["kind"] == "frontend-call"
               and item["method"] == "DELETE" and item["authoritativeOperation"].startswith("unresolved:")]

    assert all(item["risk"] == "strong" and item["confirmation"] == "strong"
               for item in deletes)


def test_admin_routes_are_inventoried_even_when_controller_name_is_not_admin():
    items = json.loads(BASELINE.read_text())["items"]
    backend = [item for item in items if item["kind"] == "backend-endpoint"]
    assessment = [item for item in backend
                  if item["authoritativeOperation"].startswith("diagnostic:AssessmentReviewController.")]

    assert len(assessment) == 14
    assert all(item["route"].startswith("/api/admin/") for item in assessment)
    assert any(item["authoritativeOperation"] == "diagnostic:AssessmentReviewController.GradeHomework"
               and item["risk"] == "ordinary" for item in assessment)


def test_literal_admin_and_hr_frontend_routes_resolve_to_backend_operations():
    items = json.loads(BASELINE.read_text())["items"]
    calls = [item for item in items if item["kind"] == "frontend-call"
             and item["route"].startswith(("/admin/", "/hr/"))]

    assert calls
    assert all(item["authoritativeOperation"].startswith("diagnostic:") for item in calls)
    subscribers = [item for item in calls if "/subscribers" in item["route"]]
    assert len(subscribers) == 8
    assert len({item["authoritativeOperation"] for item in subscribers}) == 8


def test_staff_and_whatsapp_admin_capabilities_exclude_public_webhook():
    items = json.loads(BASELINE.read_text())["items"]
    backend = [item for item in items if item["kind"] == "backend-endpoint"]
    routes = {item["route"] for item in backend}

    assert "/api/live-support/whatsapp/campaigns/{campaignid}/launch" in routes
    assert all(f"/api/live-support/whatsapp/campaigns/{{campaignid}}/{action}" in routes
               for action in ("pause", "resume", "cancel"))
    assert "/api/live-support/staff/conversations/{conversationid}/messages" in routes
    assert "/api/live-support/connections/whatsapp/{id}/disconnect" in routes
    assert "/api/exams/admin/lessons/{lessonid}/students/{studentid}/unlock" in routes
    assert "/api/video-learning/{videoid}/author" in routes
    assert "/api/video-learning/{videoid}/ai" in routes
    assert "/api/live-support/whatsapp/webhook" not in routes


def test_shared_report_service_names_all_admin_report_operations():
    items = json.loads(BASELINE.read_text())["items"]
    calls = [item for item in items if item["kind"] == "frontend-call"
             and item["source"]["file"] == "frontend/src/services/advanced-report-service.ts"
             and item["route"].startswith("/admin/reports/")]

    assert len(calls) == 9
    assert all(item["authoritativeOperation"].startswith("diagnostic:AdminReportsController.")
               for item in calls)


def test_watch_request_approval_requires_strong_confirmation_on_both_surfaces():
    items = json.loads(BASELINE.read_text())["items"]
    approvals = [item for item in items
                 if item["method"] == "POST"
                 and re.sub(r"\{[^}]+\}", "{}", item["route"].lower())
                 in {"/api/admin/watch-requests/{}/approve", "/admin/watch-requests/{}/approve"}]

    assert len(approvals) == 2
    assert all(item["risk"] == "strong" and item["confirmation"] == "strong"
               for item in approvals)
    assert all(item["domain"] == "identity" for item in approvals)
