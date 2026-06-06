import pytest
from tests.conftest import NaderGorgeClient


def grant_package_and_get_video(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")
    assert package_id is not None
    assert lesson_id is not None

    student = NaderGorgeClient()
    assert student.login("20000000001", "password").status_code == 200

    admin = NaderGorgeClient()
    assert admin.login("20000000000", "password").status_code == 200
    grant_res = admin.post("/api/e2e/grant-package", json={"packageId": package_id})
    assert grant_res.status_code == 200

    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code == 200
    videos = lesson_res.json().get("data", {}).get("videos", [])
    assert len(videos) > 0

    return student, admin, lesson_id, videos[0].get("id")


def test_video_session_does_not_expose_embed_material_and_forged_seconds_are_capped(mock_package):
    student, _admin, lesson_id, video_id = grant_package_and_get_video(mock_package)
    assert video_id is not None

    session_res = student.post("/api/student/video-session", json={"lessonVideoId": video_id})
    assert session_res.status_code == 200
    session_data = session_res.json().get("data", {})
    assert session_data.get("sessionId") is not None
    assert "token" not in session_data
    assert "key" not in session_data

    track_res = student.post("/api/tracking/video-event", json={
        "lessonVideoId": video_id,
        "watchedSeconds": 10_000,
        "totalDurationSeconds": 100
    })
    assert track_res.status_code == 200
    tracking = track_res.json().get("data", {})
    assert tracking.get("watchCount") == 1
    assert tracking.get("isLocked") is False

    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code == 200
    video_stat = lesson_res.json().get("data", {}).get("videos", [])[0]
    assert video_stat.get("watched") == 1
    assert video_stat.get("watchedSeconds") <= 30
    assert video_stat.get("isLocked") is False


def test_video_watch_limit_locks_at_exact_max_and_extra_watch_unlocks(mock_package):
    student, admin, lesson_id, video_id = grant_package_and_get_video(mock_package)
    assert video_id is not None

    track_res = student.post("/api/tracking/video-event", json={
        "lessonVideoId": video_id,
        "watchedSeconds": 10_000,
        "totalDurationSeconds": 50
    })
    assert track_res.status_code == 200
    tracking = track_res.json().get("data", {})
    assert tracking.get("watchCount") == 2
    assert tracking.get("maxWatchCount") == 2
    assert tracking.get("isLocked") is True

    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code == 200
    video_stat = lesson_res.json().get("data", {}).get("videos", [])[0]
    assert video_stat.get("watched") == 2
    assert video_stat.get("isLocked") is True

    session_fail = student.post("/api/student/video-session", json={"lessonVideoId": video_id})
    assert session_fail.status_code == 400

    req_extra = student.post(f"/api/student/video-session/{video_id}/request-extra")
    assert req_extra.status_code == 200

    req_list = admin.get("/api/admin/watch-requests")
    assert req_list.status_code == 200
    requests_data = req_list.json().get("data", [])
    assert len(requests_data) > 0

    approve_res = admin.post(f"/api/admin/watch-requests/{requests_data[0].get('id')}/approve")
    assert approve_res.status_code == 200

    session_ok = student.post("/api/student/video-session", json={"lessonVideoId": video_id})
    assert session_ok.status_code == 200
