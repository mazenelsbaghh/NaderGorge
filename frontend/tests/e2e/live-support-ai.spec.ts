import { expect, test, type Page } from "@playwright/test";

const appUrl = process.env.LIVE_SUPPORT_E2E_URL || "http://127.0.0.1:8738";
const conversationId = "14600000-0000-0000-0000-000000000001";

async function mockParticipant(page: Page, turnState: "Queued" | "Completed", messages: unknown[]) {
  const conversation = { id: conversationId, status: "Waiting", participantType: "Guest", subject: "AI test", createdAt: new Date().toISOString(), version: 1, canSend: true, canRate: false, isAiActive: true, isAiTyping: turnState === "Queued" };
  const headers = {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS, PUT, DELETE",
    "Access-Control-Allow-Headers": "Content-Type, X-App-Surface, Authorization"
  };
  await page.route("**/api/live-support/availability", route => route.fulfill({ contentType: "application/json", headers, body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 0, code: "AI_AVAILABLE", message: "متاح" } }) }));
  await page.route("**/api/live-support/participant/conversations", route => route.fulfill({ contentType: "application/json", headers, body: JSON.stringify({ success: true, data: [conversation] }) }));
  await page.route("**/api/live-support/participant/conversations/" + conversationId + "/ai/snapshot", route => route.fulfill({ contentType: "application/json", headers, body: JSON.stringify({ success: true, data: { conversationId, status: "Waiting", aiMode: "AiActive", lastSequence: messages.length, canSend: true, aiTurnState: turnState, messages, pendingDecision: null, verification: null } }) }));
}

test.describe("AI live support participant", () => {
  test.beforeEach(async ({ page }) => {
    page.on('console', msg => console.log('PARTICIPANT PAGE LOG:', msg.text()));
    page.on('pageerror', err => console.error('PARTICIPANT PAGE ERROR:', err.message));
  });

  test("AI disclosure and thinking state are explicit at 320px", async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await mockParticipant(page, "Queued", [{ id: crypto.randomUUID(), conversationId, senderType: "Guest", clientMessageId: "guest-1", type: "Text", content: "محتاج مساعدة", sentAt: new Date().toISOString() }]);
    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click();
    await expect(page.getByText("أنت تتحدث الآن مع مساعد ذكي")).toBeVisible();
    await expect(page.getByText("بنراجع رسالتك ونجهّز الرد…")).toBeVisible();
    await expect(page.getByRole("button", { name: /موظف/ })).toBeVisible();
    await expect(page.locator("[role='dialog']")).toHaveCSS("width", "288px");
  });

  test("AI reply and reconnect snapshot render once without guest data disclosure", async ({ page }) => {
    const aiReply = { id: crypto.randomUUID(), conversationId, senderType: "AI", clientMessageId: "ai-turn-1", type: "Text", content: "أهلاً، أقدر أساعدك في استفسارك.", sentAt: new Date().toISOString() };
    await mockParticipant(page, "Completed", [aiReply]);
    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click();
    await expect(page.getByRole("log").getByText(aiReply.content)).toHaveCount(1);
    await expect(page.getByRole("log")).not.toContainText(/password|token|010\d{8}/i);
    await page.reload();
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click();
    await expect(page.getByRole("log").getByText(aiReply.content)).toHaveCount(1);
  });
});

test.describe("AI live support staff and administration", () => {
  test.beforeEach(async ({ request, page }) => {
    page.on('console', msg => console.log('STAFF/ADMIN PAGE LOG:', msg.text()));
    page.on('pageerror', err => console.error('STAFF/ADMIN PAGE ERROR:', err.message));

    // Seed and login as real admin (which has both admin settings and staff access)
    const seeded = await request.post('http://127.0.0.1:5245/api/e2e/seed', {
      data: {
        clearDatabase: false,
        seedAdmin: true,
        seedStudents: true,
        seedAssistant: true,
        seedLiveSupport: true
      }
    });
    expect(seeded.ok()).toBeTruthy();

    const adminLogin = await request.post('http://127.0.0.1:5245/api/auth/login', {
      headers: { 'X-App-Surface': 'admin' },
      data: {
        phoneNumber: '20000000000',
        password: 'password',
        deviceFingerprint: 'live-support-e2e-admin'
      }
    });
    expect(adminLogin.ok()).toBeTruthy();
    const adminAuth = (await adminLogin.json()).data;

    await page.addInitScript(({ token, user }) => {
      localStorage.setItem('accessToken', token);
      localStorage.setItem('user', JSON.stringify({
        id: user.id,
        fullName: user.fullName,
        phone: user.phone,
        roles: user.roles,
        permissions: user.permissions || [],
        profileComplete: user.profileComplete,
        allowedDomains: user.allowedDomains || ["all"],
        allowedNavbarItems: user.allowedNavbarItems || ["all"]
      }));
    }, { token: adminAuth.accessToken, user: adminAuth.user });
  });

  test("staff workspace: linked student context lazy-loading and action execution", async ({ page }) => {
    const conversation = {
      id: conversationId,
      status: "Assigned",
      participantType: "Student",
      subject: "مشكلة بالرصيد",
      createdAt: new Date().toISOString(),
      version: 1,
      canSend: true,
      canRate: false,
      currentOwnerUserId: "20000000-0000-0000-0000-000000000000",
      linkedStudentUserId: "student-id-1"
    };

    const studentContext = {
      userId: "student-id-1",
      fullName: "طالب ذكي",
      phoneNumber: "01012345678",
      isActive: true,
      studentCode: "STUD-146",
      balance: 150,
      points: 200,
      devices: [],
      grants: [],
      notes: []
    };

    const actionCatalog = [
      {
        key: "student.balance.adjust",
        category: "Balance",
        labelAr: "تعديل الرصيد",
        danger: "financial",
        reasonRequired: true,
        confirmationVersion: "v1",
        refreshSections: ["balance"]
      }
    ];

    // Mock endpoints
    await page.route("**/api/live-support/staff/bootstrap", route => route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: { isCheckedIn: true, isEnabled: true, activeLoad: 1, capacity: 5, waitingCount: 0, conversations: [conversation] } })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/messages*", route => route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: [] })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/student-context", route => route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: studentContext })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/actions", route => route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: actionCatalog })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/actions/student.balance.adjust", route => route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: { message: "تم تعديل الرصيد بنجاح." } })
    }));

    await page.goto("http://127.0.0.1:8742/assistant/live-support");
    
    // Select the conversation
    await page.getByText("مشكلة بالرصيد").first().click();
    
    // Toggle Profile Context lazy loading
    await page.getByRole("button", { name: "الملف الشخصي" }).click();
    await expect(page.getByText("طالب ذكي")).toBeVisible();
    await expect(page.getByText("STUD-146")).toBeVisible();

    // Open Action Panel and select adjustment
    await page.getByRole("button", { name: "تعديل الرصيد" }).click();
    await expect(page.getByRole("dialog")).toBeVisible();
    
    // Fill adjustment reason and review
    await page.getByLabel("السبب").fill("تعويض عن درس");
    await page.getByLabel("القيمة بالجنيه").fill("50");
    await page.getByRole("button", { name: "مراجعة وتأكيد" }).click();

    // Confirm execution
    await page.getByRole("button", { name: "تأكيد تعديل الرصيد" }).click();
    await expect(page.getByRole("status")).toContainText("تم تعديل الرصيد بنجاح");
  });

  test("admin settings: policy publishing, conflict verification, and emergency disable", async ({ page }) => {
    const aiConfig = {
      draft: {
        id: "draft-id-1",
        versionNumber: 2,
        status: "Draft",
        isEnabled: false,
        systemInstructions: "أنت مساعد الدعم المباشر",
        readableDataKeys: [],
        actionKeys: [],
        lookupKeys: [],
        verificationQuestionKeys: ["profile.full_name"],
        verificationRequiredCorrect: 1,
        verificationMaxAttempts: 3,
        pendingActionExpirySeconds: 300,
        inactivityMinutes: 30,
        inactivityWarningGraceSeconds: 120,
        version: 5
      },
      published: null,
      catalogs: {
        readableData: [],
        actions: [],
        lookupKeys: [],
        verificationQuestions: [{ key: "profile.full_name", label: "الاسم الكامل", description: "الاسم المسجل" }]
      }
    };

    const stats = {
      activeConversations: 12,
      resolvedIssues: 45,
      handoffs: 8,
      totalMessagesSent: 154,
      successfulActions: 19
    };

    // Mock config endpoints
    await page.route("**/api/live-support/admin/ai/config", route => route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: aiConfig })
    }));

    await page.route("**/api/live-support/admin/ai/stats*", route => route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: stats })
    }));

    await page.route("**/api/live-support/admin/ai/active-conversations", route => route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: [] })
    }));

    await page.route("**/api/live-support/admin/ai/disable", route => route.fulfill({
      status: 202,
      contentType: "application/json",
      body: JSON.stringify({ success: true, message: "Reconciliation started" })
    }));

    await page.route("**/api/live-support/admin/ai/publish", route => {
      // Simulate conflict if wrong version
      return route.fulfill({
        status: 409,
        contentType: "application/json",
        body: JSON.stringify({ success: false, message: "VERSION_CONFLICT" })
      });
    });

    await page.goto("http://127.0.0.1:8740/admin/live-support/ai");

    // Verify stats tab
    await page.getByRole("button", { name: "الإحصائيات والنشاط" }).click();
    await expect(page.getByText("12")).toBeVisible(); // active conversations
    await expect(page.getByText("45")).toBeVisible(); // resolved
    await expect(page.getByText("19")).toBeVisible(); // successful actions

    // Return to settings
    await page.getByRole("button", { name: "إعدادات المساعد الذكي" }).click();

    // Verify publish version conflict handling
    await page.getByRole("button", { name: "نشر السياسة الحالية" }).click();
    await expect(page.getByText("VERSION_CONFLICT")).toBeVisible();

    // Verify emergency disable call
    await page.getByRole("button", { name: "إيقاف الرد الآلي فورًا" }).click();
    await expect(page.getByText("تم إيقاف الرد الآلي")).toBeVisible();
  });
});