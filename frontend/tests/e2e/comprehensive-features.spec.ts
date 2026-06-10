import { test, expect } from '@playwright/test';

test.describe('Comprehensive Features E2E Tests', () => {
  let studentToken: string;
  let adminToken: string;
  let lessonId: string;

  test.beforeAll(async ({ request }) => {
    // 1. Seed database
    await request.post('http://localhost:5245/api/e2e/seed', {
      data: {
        clearDatabase: true,
        seedAdmin: true,
        seedStudents: true,
        seedAssistant: true,
        seedTeacher: true,
      },
    });

        // 2. Setup mock package
    const setupResponse = await request.post(
      'http://localhost:5245/api/e2e/setup-mock-package'
    );
    expect(setupResponse.ok()).toBeTruthy();
    const mockData = await setupResponse.json();
    lessonId = mockData.lessonId;

    // Grant package to Student 1
    const grantResponse = await request.post('http://localhost:5245/api/e2e/grant-package', {
      data: { packageId: mockData.packageId },
    });
    expect(grantResponse.ok()).toBeTruthy();

    // 3. Log in as Student and acquire token
    const studentLogin = await request.post('http://localhost:5245/api/auth/login', {
      headers: { 'X-App-Surface': 'student' },
      data: {
        phoneNumber: '20000000001',
        password: 'password',
        deviceFingerprint: 'e2e-fingerprint-student',
        deviceName: 'E2E Browser',
      },
    });
    expect(studentLogin.ok()).toBeTruthy();
    studentToken = (await studentLogin.json()).data.accessToken;

    // 4. Log in as Admin and acquire token
    const adminLogin = await request.post('http://localhost:5245/api/auth/login', {
      headers: { 'X-App-Surface': 'admin' },
      data: {
        phoneNumber: '20000000000',
        password: 'password',
        deviceFingerprint: 'e2e-fingerprint-admin',
        deviceName: 'E2E Browser',
      },
    });
    expect(adminLogin.ok()).toBeTruthy();
    adminToken = (await adminLogin.json()).data.accessToken;
  });

  test('Student Theme Preference - Retrieve and Update', async ({ request }) => {
    // 1. Get initial theme preferences
    const getRes = await request.get('http://localhost:5245/api/student/theme-preferences', {
      headers: { Authorization: `Bearer ${studentToken}` },
    });
    expect(getRes.ok()).toBeTruthy();
    const initialPrefs = await getRes.json();
    expect(initialPrefs.success).toBe(true);

    // 2. Update preferences
    const updateRes = await request.put('http://localhost:5245/api/student/theme-preferences', {
      headers: { Authorization: `Bearer ${studentToken}` },
      data: {
        lightPaletteId: 'oasis-light',
        darkPaletteId: 'midnight-teal',
        currentMode: 'dark',
        avatarSlug: 'avatar-student-3',
      },
    });
    expect(updateRes.ok()).toBeTruthy();

    // 3. Retrieve and assert updated preferences
    const getUpdatedRes = await request.get('http://localhost:5245/api/student/theme-preferences', {
      headers: { Authorization: `Bearer ${studentToken}` },
    });
    expect(getUpdatedRes.ok()).toBeTruthy();
    const updatedPrefs = await getUpdatedRes.json();
    expect(updatedPrefs.data.selectedLightPaletteId).toBe('oasis-light');
    expect(updatedPrefs.data.selectedDarkPaletteId).toBe('midnight-teal');
    expect(updatedPrefs.data.currentMode).toBe('dark');

    // 4. Verify updated avatarSlug on dashboard
    const dashboardRes = await request.get('http://localhost:5245/api/student/dashboard', {
      headers: { Authorization: `Bearer ${studentToken}` },
    });
    expect(dashboardRes.ok()).toBeTruthy();
    const dashboardData = await dashboardRes.json();
    expect(dashboardData.data.avatarSlug).toBe('avatar-student-3');
  });

  test('Lesson Comments Moderation Flow', async ({ request }) => {
    // 1. Student leaves a comment on the lesson
    const createCommentRes = await request.post(
      `http://localhost:5245/api/content/lessons/${lessonId}/comments`,
      {
        headers: { Authorization: `Bearer ${studentToken}` },
        data: { body: 'This is a test comment by student' },
      }
    );
    expect(createCommentRes.ok()).toBeTruthy();
    const commentData = await createCommentRes.json();
    const commentId = commentData.data.id;
    expect(commentId).toBeDefined();

    // 2. Admin retrieves lesson comments for moderation
    const getModerationCommentsRes = await request.get(
      `http://localhost:5245/api/admin/lessons/${lessonId}/comments`,
      {
        headers: { Authorization: `Bearer ${adminToken}` },
      }
    );
    expect(getModerationCommentsRes.ok()).toBeTruthy();
    const moderationComments = await getModerationCommentsRes.json();
    expect(moderationComments.success).toBe(true);
    const commentToModerate = moderationComments.data.find(
      (c: any) => c.id === commentId
    );
    expect(commentToModerate).toBeDefined();

    // 3. Admin approves the comment
    const approveRes = await request.post(
      `http://localhost:5245/api/admin/comments/${commentId}/approve`,
      {
        headers: { Authorization: `Bearer ${adminToken}` },
      }
    );
    expect(approveRes.ok()).toBeTruthy();
  });

  test('Student Community Posts Flow', async ({ request }) => {
    // 1. Student creates a post
    const createPostRes = await request.post('http://localhost:5245/api/community/posts', {
      headers: { Authorization: `Bearer ${studentToken}` },
      data: {
        body: 'Hello Community!',
        pollOptions: [],
      },
    });
    expect(createPostRes.ok()).toBeTruthy();
    const postData = await createPostRes.json();
    const postId = postData.data.id;
    expect(postId).toBeDefined();

    // 2. Admin gets posts for moderation
    const getPostsModRes = await request.get(
      'http://localhost:5245/api/admin/community/posts',
      {
        headers: { Authorization: `Bearer ${adminToken}` },
      }
    );
    expect(getPostsModRes.ok()).toBeTruthy();
    const postsMod = await getPostsModRes.json();
    expect(postsMod.success).toBe(true);

    // 3. Admin approves the post
    const approvePostRes = await request.post(
      `http://localhost:5245/api/admin/community/posts/${postId}/approve`,
      {
        headers: { Authorization: `Bearer ${adminToken}` },
      }
    );
    expect(approvePostRes.ok()).toBeTruthy();

    // 4. Student likes the post (post must be approved first)
    const likeRes = await request.post(
      `http://localhost:5245/api/community/posts/${postId}/likes/toggle`,
      {
        headers: { Authorization: `Bearer ${studentToken}` },
      }
    );
    expect(likeRes.ok()).toBeTruthy();

    // 5. Student comments on the post
    const createCommentRes = await request.post(
      `http://localhost:5245/api/community/posts/${postId}/comments`,
      {
        headers: { Authorization: `Bearer ${studentToken}` },
        data: { body: 'This is a community post comment' },
      }
    );
    expect(createCommentRes.ok()).toBeTruthy();
    const postCommentData = await createCommentRes.json();
    const commentId = postCommentData.data.id;
    expect(commentId).toBeDefined();
  });
});
