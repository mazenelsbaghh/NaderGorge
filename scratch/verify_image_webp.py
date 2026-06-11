import os
import sys
import requests

# Add root folder to sys.path so we can import tests
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from tests.conftest import NaderGorgeClient, BASE_URL, e2e_headers

def run_verification():
    print("Initializing DB...")
    # Trigger DB seed directly
    res = requests.post(f"{BASE_URL}/api/e2e/seed", json={
        "clearDatabase": True,
        "seedAdmin": True,
        "seedStudents": True,
        "seedAssistant": True,
        "seedTeacher": True
    }, headers=e2e_headers())
    assert res.status_code == 200, f"Seed failed: {res.text}"
    print("DB Seed Completed successfully!")
    
    # 1. Admin login
    print("Logging in as Admin...")
    admin = NaderGorgeClient()
    login_res = admin.login("20000000000", "password")
    assert login_res.status_code == 200, "Admin login failed"
    
    # Fetch teachers to find the teacher's ID
    teachers_res = admin.get("/api/admin/teachers")
    assert teachers_res.status_code == 200, "Failed to get teachers list"
    teachers = teachers_res.json().get("data", [])
    assert len(teachers) > 0, "No teachers found in seeded database"
    teacher = teachers[0]
    teacher_id = teacher.get("id")
    user_id = teacher.get("userId")
    print(f"Found Teacher Profile ID: {teacher_id}, User ID: {user_id}")
    
    png_base64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
    jpg_base64 = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA="
    webp_base64 = "data:image/webp;base64,UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA=="
    
    # --- Test 1: Admin Upload Teacher Profile Image ---
    print("\n[Test 1] Admin Upload Profile Image...")
    res = admin.post("/api/admin/teachers/upload-profile-image", json={
        "teacherId": teacher_id,
        "base64Image": webp_base64,
        "fileName": "avatar.png"
    })
    assert res.status_code == 200, f"Upload profile image failed: {res.text}"
    img_url = res.json().get("data")
    print(f"Profile Image URL: {img_url}")
    assert img_url.endswith(".webp"), f"Image URL does not end with .webp: {img_url}"
    
    # --- Test 2: Admin Upload Teacher AI Photo ---
    print("\n[Test 2] Admin Upload AI Photo...")
    res = admin.post("/api/admin/teacher-photos/upload", json={
        "teacherId": user_id,
        "base64Image": webp_base64,
        "fileName": "photo.jpg"
    })
    assert res.status_code == 200, f"Upload AI photo failed: {res.text}"
    ai_photo_res = res.json()
    print(f"AI Photo response: {ai_photo_res}")
    
    # 2. Teacher Login
    print("\nLogging in as Teacher...")
    teacher_client = NaderGorgeClient()
    login_res = teacher_client.login("20000000004", "password")
    assert login_res.status_code == 200, "Teacher login failed"
    
    # --- Test 3: Teacher Upload Profile Image ---
    print("\n[Test 3] Teacher Upload Profile Image...")
    res = teacher_client.post("/api/teacher/profile/upload-image", json={
        "base64Image": webp_base64,
        "fileName": "my-avatar.jpeg"
    })
    assert res.status_code == 200, f"Teacher upload profile image failed: {res.text}"
    img_url = res.json().get("data")
    print(f"Teacher Profile Image URL: {img_url}")
    assert img_url.endswith(".webp"), f"Image URL does not end with .webp: {img_url}"
    
    # --- Test 4: Teacher Upload AI Photo ---
    print("\n[Test 4] Teacher Upload AI Photo...")
    res = teacher_client.post("/api/teacher/profile/upload-ai-photo", json={
        "base64Image": webp_base64,
        "fileName": "my-ai.png"
    })
    assert res.status_code == 200, f"Teacher upload AI photo failed: {res.text}"
    print("Teacher Upload AI Photo Succeeded!")

    print("\n🎉 ALL MANUAL VERIFICATION TESTS PASSED SUCCESSFULLY! 🎉")

if __name__ == "__main__":
    run_verification()
