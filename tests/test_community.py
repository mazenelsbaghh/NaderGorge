import pytest
from tests.conftest import NaderGorgeClient

def test_community_post_moderation_and_interactions(clean_db):
    # Setup clients
    student1 = NaderGorgeClient()
    student1.login("20000000001", "password")

    student2 = NaderGorgeClient(fingerprint="e2e-dev1")
    student2.login("20000000002", "password")

    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    # Step 1: Student 1 creates a community post with poll options
    post_res = student1.post("/api/community/posts", json={
        "body": "This is a poll post created by Student 1.",
        "pollOptions": ["Option A", "Option B"]
    })
    assert post_res.status_code == 200
    post_data = post_res.json().get("data", {})
    post_id = post_data.get("id")
    assert post_id is not None
    assert post_data.get("status") == "Pending"

    # Step 2: Verify post is NOT in the public posts list for Student 2
    posts_res = student2.get("/api/community/posts")
    assert posts_res.status_code == 200
    public_posts = posts_res.json().get("data", [])
    assert not any(p.get("id") == post_id for p in public_posts)

    # Step 3: Admin retrieves pending posts and finds our post
    pending_res = admin.get("/api/admin/community/posts?status=Pending")
    assert pending_res.status_code == 200
    pending_posts = pending_res.json().get("data", [])
    assert any(p.get("id") == post_id for p in pending_posts)

    # Step 4: Admin approves the post
    approve_res = admin.post(f"/api/admin/community/posts/{post_id}/approve")
    assert approve_res.status_code == 200

    # Step 5: Verify post IS now in the public posts list for Student 2
    posts_res2 = student2.get("/api/community/posts")
    assert posts_res2.status_code == 200
    public_posts2 = posts_res2.json().get("data", [])
    matching_posts = [p for p in public_posts2 if p.get("id") == post_id]
    assert len(matching_posts) > 0
    matching_post = matching_posts[0]
    assert matching_post is not None

    # Step 6: Test Toggle Like
    # Student 2 likes the post
    like_res = student2.post(f"/api/community/posts/{post_id}/likes/toggle")
    assert like_res.status_code == 200
    # Retrieve public posts list to check likes count
    posts_res3 = student2.get("/api/community/posts")
    matching_post = [p for p in posts_res3.json().get("data", []) if p.get("id") == post_id][0]
    assert matching_post.get("likeCount") == 1

    # Student 2 toggles like again (removes like)
    like_res2 = student2.post(f"/api/community/posts/{post_id}/likes/toggle")
    assert like_res2.status_code == 200
    posts_res4 = student2.get("/api/community/posts")
    matching_post = [p for p in posts_res4.json().get("data", []) if p.get("id") == post_id][0]
    assert matching_post.get("likeCount") == 0

    # Step 7: Test Poll Voting
    # Find option IDs
    poll_options = matching_post.get("pollOptions", [])
    assert len(poll_options) == 2
    opt_a = [o for o in poll_options if o.get("text") == "Option A"][0]
    opt_a_id = opt_a.get("id")
    opt_b = [o for o in poll_options if o.get("text") == "Option B"][0]
    opt_b_id = opt_b.get("id")

    # Student 2 votes for Option A
    vote_res = student2.post(f"/api/community/posts/{post_id}/polls/{opt_a_id}/vote")
    assert vote_res.status_code == 200
    vote_data = vote_res.json().get("data", {})
    assert vote_data.get("optionIdSelected") == opt_a_id
    assert vote_data.get("optionVoteCounts", {}).get(opt_a_id) == 1

    # Student 2 votes for Option A again (removes vote)
    vote_res2 = student2.post(f"/api/community/posts/{post_id}/polls/{opt_a_id}/vote")
    assert vote_res2.status_code == 200
    vote_data2 = vote_res2.json().get("data", {})
    assert vote_data2.get("optionIdSelected") is None
    assert vote_data2.get("optionVoteCounts", {}).get(opt_a_id) == 0

    # Student 2 votes for Option A, then switches to Option B
    student2.post(f"/api/community/posts/{post_id}/polls/{opt_a_id}/vote")
    vote_res3 = student2.post(f"/api/community/posts/{post_id}/polls/{opt_b_id}/vote")
    assert vote_res3.status_code == 200
    vote_data3 = vote_res3.json().get("data", {})
    assert vote_data3.get("optionIdSelected") == opt_b_id
    assert vote_data3.get("optionVoteCounts", {}).get(opt_a_id) == 0
    assert vote_data3.get("optionVoteCounts", {}).get(opt_b_id) == 1

    # Step 8: Test Comment Moderation
    # Student 2 creates a comment on the post
    comment_res = student2.post(f"/api/community/posts/{post_id}/comments", json={
        "body": "This is a comment on the post."
    })
    assert comment_res.status_code == 200
    comment_data = comment_res.json().get("data", {})
    comment_id = comment_data.get("id")
    assert comment_id is not None
    assert comment_data.get("status") == "Pending"

    # Verify comment is NOT in public comments list for Student 1
    comments_res = student1.get(f"/api/community/posts/{post_id}/comments")
    assert comments_res.status_code == 200
    public_comments = comments_res.json().get("data", [])
    assert not any(c.get("id") == comment_id for c in public_comments)

    # Admin retrieves pending comments
    pending_comments_res = admin.get("/api/admin/community/comments/pending")
    assert pending_comments_res.status_code == 200
    pending_comments = pending_comments_res.json().get("data", [])
    assert any(c.get("id") == comment_id for c in pending_comments)

    # Admin approves comment
    approve_comment_res = admin.post(f"/api/admin/community/comments/{comment_id}/approve")
    assert approve_comment_res.status_code == 200

    # Verify comment IS now in public comments list for Student 1
    comments_res2 = student1.get(f"/api/community/posts/{post_id}/comments")
    assert comments_res2.status_code == 200
    public_comments2 = comments_res2.json().get("data", [])
    assert any(c.get("id") == comment_id for c in public_comments2)
