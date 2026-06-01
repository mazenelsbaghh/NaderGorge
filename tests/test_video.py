import pytest
from tests.conftest import NaderGorgeClient

def test_video_session_tracking_and_extra_watches(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")

    # Login as Student 1
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Grant package to Student 1 so they can view details
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")
    admin.post("/api/e2e/grant-package", json={"packageId": package_id, "userId": student.login("20000000001", "password").json().get("data", {}).get("user", {}).get("id")})

    # Retrieve lesson details to find the video ID
    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code == 200
    videos = lesson_res.json().get("data", {}).get("videos", [])
    assert len(videos) > 0
    video = videos[0]
    video_id = video.get("id")
    assert video_id is not None

    # Step 1: Create playback session
    session_res = student.post("/api/student/video-session", json={"lessonVideoId": video_id})
    assert session_res.status_code == 200
    session_data = session_res.json().get("data", {})
    session_id = session_data.get("sessionId")
    assert session_id is not None

    # Step 2: Consume playback session
    consume_res = student.post(f"/api/student/video-session/{session_id}/consume")
    assert consume_res.status_code == 200

    # Step 3: Send video watched event under threshold (threshold is typically 30% or 85%, total duration is 100 seconds)
    track_res = student.post("/api/tracking/video-event", json={
        "lessonVideoId": video_id,
        "watchedSeconds": 5,
        "totalDurationSeconds": 100
    })
    assert track_res.status_code == 200

    # Get watch stats from lesson details
    lesson_res2 = student.get(f"/api/content/lessons/{lesson_id}")
    video_stat = lesson_res2.json().get("data", {}).get("videos", [])[0]
    assert video_stat.get("watched") == 0

    # Step 4: Send watched event that triggers first watch count increment (watched 90 seconds out of 100)
    track_res2 = student.post("/api/tracking/video-event", json={
        "lessonVideoId": video_id,
        "watchedSeconds": 90,
        "totalDurationSeconds": 100
    })
    assert track_res2.status_code == 200

    # Verify watch count is 1
    lesson_res3 = student.get(f"/api/content/lessons/{lesson_id}")
    video_stat = lesson_res3.json().get("data", {}).get("videos", [])[0]
    assert video_stat.get("watched") == 1

    # Step 5: Trigger second watch increment (another 90 seconds, cumulative 180 seconds)
    track_res3 = student.post("/api/tracking/video-event", json={
        "lessonVideoId": video_id,
        "watchedSeconds": 90,
        "totalDurationSeconds": 100
    })
    assert track_res3.status_code == 200

    # Verify watch count is 2 (MaxWatchCount in setup-mock-package is 2)
    lesson_res4 = student.get(f"/api/content/lessons/{lesson_id}")
    video_stat = lesson_res4.json().get("data", {}).get("videos", [])[0]
    assert video_stat.get("watched") == 2

    # Step 5b: Trigger third watch increment to lock the video (another 90 seconds, cumulative 270 seconds)
    track_res4 = student.post("/api/tracking/video-event", json={
        "lessonVideoId": video_id,
        "watchedSeconds": 90,
        "totalDurationSeconds": 100
    })
    assert track_res4.status_code == 200

    # Verify watch count is 3
    lesson_res5 = student.get(f"/api/content/lessons/{lesson_id}")
    video_stat = lesson_res5.json().get("data", {}).get("videos", [])[0]
    assert video_stat.get("watched") == 3
    assert video_stat.get("isLocked") is True

    # Step 6: Next session creation should fail
    session_fail = student.post("/api/student/video-session", json={"lessonVideoId": video_id})
    assert session_fail.status_code == 400

    # Step 7: Request extra watch session
    req_extra = student.post(f"/api/student/video-session/{video_id}/request-extra")
    assert req_extra.status_code == 200

    # Step 8: Admin list and approve extra watch request
    req_list = admin.get("/api/admin/watch-requests")
    assert req_list.status_code == 200
    requests_data = req_list.json().get("data", [])
    assert len(requests_data) > 0
    # Find request corresponding to student
    target_request = requests_data[0]
    req_id = target_request.get("id")

    approve_res = admin.post(f"/api/admin/watch-requests/{req_id}/approve")
    assert approve_res.status_code == 200

    # Step 9: Verify video session can be created again!
    session_ok = student.post("/api/student/video-session", json={"lessonVideoId": video_id})
    assert session_ok.status_code == 200
