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

    # Ensure it doesn't immediately relock on first watch progress sync of the new watch session
    track_sync = student.post("/api/tracking/video-event", json={
        "lessonVideoId": video_id,
        "watchedSeconds": 5,
        "totalDurationSeconds": 50
    })
    assert track_sync.status_code == 200
    tracking_sync = track_sync.json().get("data", {})
    assert tracking_sync.get("watchCount") == 2
    assert tracking_sync.get("maxWatchCount") == 3
    assert tracking_sync.get("isLocked") is False

    # Sleep to allow elapsed time check to accept 15 seconds
    import time
    time.sleep(10)

    # Ensure it locks if we watch past the new limit (3 watches, threshold at 30% of 50s = 15s)
    # The current watch event has WatchCount = 2, so threshold for 3rd watch is 3 * 15 = 45s.
    # Total watched so far is 2 * 15 = 30s + 5s = 35s.
    # We watch 15 more seconds (total 50s), which is >= 45s.
    track_relock = student.post("/api/tracking/video-event", json={
        "lessonVideoId": video_id,
        "watchedSeconds": 15,
        "totalDurationSeconds": 50
    })
    assert track_relock.status_code == 200
    tracking_relock = track_relock.json().get("data", {})
    assert tracking_relock.get("watchCount") == 3
    assert tracking_relock.get("maxWatchCount") == 3
    assert tracking_relock.get("isLocked") is True

    # Now it is locked again (WatchCount = 3, MaxWatchCount = 3).
    # Request extra watch (2nd request)
    req_extra2 = student.post(f"/api/student/video-session/{video_id}/request-extra")
    assert req_extra2.status_code == 200

    # Approve it
    req_list2 = admin.get("/api/admin/watch-requests")
    assert req_list2.status_code == 200
    pending_reqs2 = [r for r in req_list2.json().get("data", []) if r.get("status") == 0 or r.get("status") == "Pending"]
    assert len(pending_reqs2) > 0
    approve_res2 = admin.post(f"/api/admin/watch-requests/{pending_reqs2[0].get('id')}/approve")
    assert approve_res2.status_code == 200

    # Request extra watch (3rd request)
    req_extra3 = student.post(f"/api/student/video-session/{video_id}/request-extra")
    assert req_extra3.status_code == 200

    # Reject it
    req_list3 = admin.get("/api/admin/watch-requests")
    assert req_list3.status_code == 200
    pending_reqs3 = [r for r in req_list3.json().get("data", []) if r.get("status") == 0 or r.get("status") == "Pending"]
    assert len(pending_reqs3) > 0
    reject_res3 = admin.post(f"/api/admin/watch-requests/{pending_reqs3[0].get('id')}/reject", json={"reason": "Exceeded limit test"})
    assert reject_res3.status_code == 200

    # Request extra watch (4th request) - this should fail because we reached the limit of 3 requests
    req_extra4 = student.post(f"/api/student/video-session/{video_id}/request-extra")
    assert req_extra4.status_code == 400
    assert "REQUEST_LIMIT_REACHED" in req_extra4.json().get("errors", [])
