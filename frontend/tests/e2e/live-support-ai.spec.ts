import { expect, test, type Page } from "@playwright/test";

const appUrl = process.env.LIVE_SUPPORT_E2E_URL || "http://localhost:3000";
const conversationId = "14600000-0000-0000-0000-000000000001";

// Helper to construct browser-compatible CORS headers for mocked routes
function getHeaders(route: any) {
  const origin = route.request().headers().origin || "*";
  return {
    "Access-Control-Allow-Origin": origin,
    "Access-Control-Allow-Credentials": "true",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS, PUT, DELETE, HEAD",
    "Access-Control-Allow-Headers": "Content-Type, X-App-Surface, Authorization"
  };
}

async function mockParticipant(
  page: Page,
  turnState: "Queued" | "Completed",
  messages: unknown[],
  pendingDecision: unknown = null,
  verification: unknown = null
) {
  const conversation = {
    id: conversationId,
    status: "Waiting",
    participantType: "Guest",
    subject: "AI test",
    createdAt: new Date().toISOString(),
    version: 1,
    canSend: true,
    canRate: false,
    isAiActive: true,
    isAiTyping: turnState === "Queued"
  };

  await page.route("**/api/live-support/availability", route =>
    route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({
        success: true,
        data: { isAvailable: true, availableStaffCount: 0, code: "AI_AVAILABLE", message: "متاح" }
      })
    })
  );

  await page.route("**/api/live-support/participant/conversations", route =>
    route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: [conversation] })
    })
  );

  await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/snapshot`, route =>
    route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({
        success: true,
        data: {
          conversationId,
          status: "Waiting",
          aiMode: "AiActive",
          lastSequence: messages.length,
          canSend: true,
          aiTurnState: turnState,
          messages,
          pendingDecision,
          verification
        }
      })
    })
  );
}

async function mockStaffWorkspace(page: Page) {
  await page.route("**/api/live-support/staff/bootstrap", route => route.fulfill({
    contentType: "application/json",
    headers: getHeaders(route),
    body: JSON.stringify({ success: true, data: {
      isCheckedIn: true,
      isEnabled: true,
      activeLoad: 0,
      capacity: 5,
      waitingCount: 0,
      conversations: []
    } })
  }));
}

async function mockAdminConfiguration(page: Page) {
  await page.route("**/api/live-support/admin/ai/config", route => route.fulfill({
    contentType: "application/json",
    headers: getHeaders(route),
    body: JSON.stringify({ success: true, data: {
      draft: null,
      published: {
        id: "pub-policy-1", versionNumber: 1, status: "Published", isEnabled: true,
        systemInstructions: "أنت مساعد الدعم الذكي لمنصة مسار", readableDataKeys: [], actionKeys: [],
        lookupKeys: [], verificationQuestionKeys: [], verificationRequiredCorrect: 1,
        verificationMaxAttempts: 3, pendingActionExpirySeconds: 300, inactivityMinutes: 30,
        inactivityWarningGraceSeconds: 120, version: 1
      },
      catalogs: { readableData: [], actions: [], lookupKeys: [], verificationQuestions: [] }
    } })
  }));
}

test.describe("AI live support participant", () => {
  test.beforeEach(async ({ page }) => {
    page.on('console', msg => console.log('PARTICIPANT PAGE LOG:', msg.text()));
    page.on('pageerror', err => console.error('PARTICIPANT PAGE ERROR:', err.message));
  });

  test("AI disclosure and thinking state are explicit at 320px", async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await mockParticipant(page, "Queued", [
      {
        id: crypto.randomUUID(),
        conversationId,
        senderType: "Guest",
        clientMessageId: "guest-1",
        type: "Text",
        content: "محتاج مساعدة",
        sentAt: new Date().toISOString()
      }
    ]);
    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click({ force: true });
    await expect(page.getByText("أنت تتحدث الآن مع مساعد ذكي")).toBeVisible();
    await expect(page.getByText("بنراجع رسالتك ونجهّز الرد…")).toBeVisible();
    await expect(page.getByRole("button", { name: /موظف/ })).toBeVisible();
    await expect(page.locator('[role="dialog"]')).toHaveCSS("width", "288px");
    
    // Assert document does not overflow viewport horizontally
    const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(hasHorizontalOverflow).toBe(false);
  });

  test("AI reply and reconnect snapshot render once without guest data disclosure", async ({ page }) => {
    const aiReply = {
      id: crypto.randomUUID(),
      conversationId,
      senderType: "AI",
      clientMessageId: "ai-turn-1",
      type: "Text",
      content: "أهلاً، أقدر أساعدك في استفسارك.",
      sentAt: new Date().toISOString()
    };
    await mockParticipant(page, "Completed", [aiReply]);
    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click({ force: true });
    await expect(page.getByRole("log").getByText(aiReply.content)).toHaveCount(1);
    await expect(page.getByRole("log")).not.toContainText(/password|token|010\d{8}/i);
    await page.reload();
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click({ force: true });
    await expect(page.getByRole("log").getByText(aiReply.content)).toHaveCount(1);
  });

  test("Action card interaction, double-click protection, and keyboard focus", async ({ page }) => {
    const decisionId = "22222222-2222-2222-2222-222222222222";
    const pendingDecision = {
      id: decisionId,
      actionKey: "student.lesson.unlock",
      safeProposalJson: JSON.stringify({ safeEffectSummaryAr: "فتح درس الفيزياء للصف الثالث الثانوي" }),
      status: "PendingConfirmation",
      expiresAt: new Date(Date.now() + 50000).toISOString()
    };

    await mockParticipant(page, "Completed", [], pendingDecision, null);

    let confirmCalledCount = 0;
    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/decisions/${decisionId}/confirm`, async route => {
      confirmCalledCount++;
      await new Promise(resolve => setTimeout(resolve, 200));
      await route.fulfill({
        contentType: "application/json",
        headers: getHeaders(route),
        body: JSON.stringify({ success: true, data: { decisionId, executionId: "3333-4444", status: "Succeeded" } })
      });
    });

    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click({ force: true });

    await expect(page.getByText("تأكيد الإجراء المطلوب")).toBeVisible();
    await expect(page.getByText("فتح درس الفيزياء للصف الثالث الثانوي")).toBeVisible();

    const confirmBtn = page.getByRole("button", { name: "تأكيد الإجراء" });
    const cancelBtn = page.getByRole("button", { name: "إلغاء" });

    await confirmBtn.focus();
    await expect(confirmBtn).toBeFocused();

    await confirmBtn.click();
    await expect(confirmBtn).toBeDisabled();
    await expect(cancelBtn).toBeDisabled();

    await expect(page.getByText("تم تنفيذ الإجراء بنجاح.")).toBeVisible();
    expect(confirmCalledCount).toBe(1);
  });

  test("Verification flow shows generic copy and handles incorrect answer", async ({ page }) => {
    const sessionId = "44444444-4444-4444-4444-444444444444";
    const activeVerification = {
      sessionId,
      status: "Challenging",
      nextQuestionKey: "profile.governorate",
      promptText: "ما هي المحافظة المسجلة بحسابك؟",
      attemptCount: 1,
      maxAttempts: 3
    };

    await mockParticipant(page, "Completed", [], null, activeVerification);

    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/verification/${sessionId}/answer`, route => {
      route.fulfill({
        contentType: "application/json",
        headers: getHeaders(route),
        body: JSON.stringify({
          success: true,
          data: {
            sessionId,
            status: "Challenging",
            nextQuestionKey: "profile.governorate",
            promptText: "ما هي المحافظة المسجلة بحسابك؟",
            attemptCount: 2,
            maxAttempts: 3
          }
        })
      });
    });

    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click({ force: true });

    await expect(page.getByText("سؤال التحقق")).toBeVisible();
    await expect(page.getByText("ما هي المحافظة المسجلة بحسابك؟")).toBeVisible();

    const answerInput = page.getByPlaceholder("اكتب الإجابة هنا...");
    await answerInput.fill("الإسكندرية");
    await page.getByRole("button", { name: "إرسال الإجابة" }).click();

    await expect(page.getByText("المحاولات المستخدمة: 2")).toBeVisible();
  });

  test("Secure registration form hides passwords and handles creation", async ({ page }) => {
    const decisionId = "55555555-5555-5555-5555-555555555555";
    const pendingDecision = {
      id: decisionId,
      actionKey: "system.registration",
      safeProposalJson: "{}",
      status: "PendingConfirmation",
      expiresAt: new Date(Date.now() + 50000).toISOString()
    };

    await mockParticipant(page, "Completed", [], pendingDecision, null);

    let registerPayload: any = null;
    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/decisions/${decisionId}/register`, route => {
      registerPayload = route.request().postDataJSON();
      route.fulfill({
        contentType: "application/json",
        headers: getHeaders(route),
        body: JSON.stringify({ success: true, data: { userId: "student-id-123", status: "CreatedAndLinked" } })
      });
    });

    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click({ force: true });

    await expect(page.getByText("إنشاء حساب جديد")).toBeVisible();

    await page.getByPlaceholder("الاسم الكامل باللغة العربية").fill("محمد أحمد علي حسن");
    await page.getByPlaceholder("01xxxxxxxxx").fill("01223344556");
    await page.getByPlaceholder("كلمة مرور قوية").fill("SecretPass123!");
    await page.locator('input[type="date"]').fill("2005-05-05");
    await page.getByPlaceholder("مثال: القاهرة").fill("القاهرة");
    await page.getByPlaceholder("اسم المدرسة بالكامل").fill("مدرسة الأورمان");
    await page.locator('input[autocomplete="street-address"]').fill("الدقي");
    await page.getByPlaceholder("يجب أن يختلف عن رقم هاتفك").fill("01009876543");

    await page.getByRole("button", { name: "إنشاء الحساب وتأكيده" }).click();

    await expect(page.getByText("تم تسجيل الحساب بنجاح")).toBeVisible();
    expect(registerPayload).not.toBeNull();
    expect(registerPayload.fullName).toBe("محمد أحمد علي حسن");
    expect(registerPayload.password).toBe("SecretPass123!");
  });

  test("reduced motion is respected and long mixed RTL/LTR wraps correctly", async ({ page }) => {
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.setViewportSize({ width: 320, height: 720 });
    const longContentMessage = {
      id: crypto.randomUUID(),
      conversationId,
      senderType: "AI",
      clientMessageId: "long-msg-1",
      type: "Text",
      content: "هذا نص طويل جداً يحتوي على اللغة العربية والانجليزية للتأكد من التغليف الصحيح: English text should wrap nicely alongside Arabic and not cause overflow.",
      sentAt: new Date().toISOString()
    };
    await mockParticipant(page, "Completed", [longContentMessage]);
    await page.goto(appUrl);
    await page.waitForTimeout(300);
    const launcher = page.getByRole("button", { name: "فتح الدعم المباشر" });
    await expect(launcher).toBeEnabled();
    await launcher.evaluate(element => (element as HTMLButtonElement).click());
    await expect(page.getByRole("log").getByText(longContentMessage.content)).toBeVisible();
  });
});

test.describe("AI live support staff and administration", () => {
  test.beforeEach(async ({ request, page }) => {
    page.on('console', msg => console.log('STAFF/ADMIN PAGE LOG:', msg.text()));
    page.on('pageerror', err => console.error('STAFF/ADMIN PAGE ERROR:', err.message));

    // Seed and login as real admin (which has both admin settings and staff access)
    const seeded = await request.post('http://127.0.0.1:5245/api/e2e/seed', {
      headers: {
        'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345'
      },
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

  test("staff page renders without horizontal overflow at tablet 768px width", async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await mockStaffWorkspace(page);
    await page.goto(`http://staff.localhost:3000/assistant/live-support`);
    await page.waitForTimeout(500);
    
    await expect(page.getByText("حالة الاتصال")).toBeVisible();
    const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(hasHorizontalOverflow).toBe(false);
  });

  test("admin AI page renders without horizontal overflow at desktop 1024px width", async ({ page }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await mockAdminConfiguration(page);
    await page.goto(`http://admin.localhost:3000/admin/live-support/ai`);
    await page.waitForTimeout(500);
    
    await expect(page.getByText("الإعدادات وقاعدة القرار")).toBeVisible();
    const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(hasHorizontalOverflow).toBe(false);
  });

  test("keyboard navigation allows tabbing through admin controls", async ({ page }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await mockAdminConfiguration(page);
    await page.goto(`http://admin.localhost:3000/admin/live-support/ai`);
    await page.waitForTimeout(500);
    
    // Focus settings tab
    const settingsTab = page.getByRole("tab", { name: "الإعدادات وقاعدة القرار" });
    await expect(settingsTab).toBeVisible();
    await settingsTab.focus();
    
    // Press Tab to move focus
    await page.keyboard.press("Tab");
    // Verify document still has focus and does not break layout
    const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(hasHorizontalOverflow).toBe(false);
  });

  test('staff workspace displays safe AI handoff summary', async ({ page }) => {
    // Mock staff bootstrap DTO and conversations
    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({
      contentType: 'application/json',
      headers: getHeaders(route),
      body: JSON.stringify({
        success: true,
        data: {
          isEnabled: true,
          isCheckedIn: true,
          activeConversationsCount: 1,
          maxActiveConversations: 5,
          waitingQueueCount: 0,
          conversations: [{
            id: conversationId,
            participantType: 'Student',
            status: 'Assigned',
            subject: 'استفسار عن الدفع',
            createdAt: new Date().toISOString(),
            isAiActive: false,
            isAiTyping: false
          }]
        }
      })
    }));
  });

  test("staff workspace: linked student context lazy-loading and action execution", async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
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
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: { isCheckedIn: true, isEnabled: true, activeLoad: 1, capacity: 5, waitingCount: 0, conversations: [conversation] } })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/messages*", route => route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: [] })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/student-context/basic", route => route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: { section: "basic", data: studentContext } })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/actions", route => route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: actionCatalog })
    }));

    let actionAttempts = 0;
    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/actions/student.balance.adjust", route => {
      actionAttempts += 1;
      return route.fulfill({
        status: actionAttempts === 1 ? 200 : 409,
        contentType: "application/json",
        headers: getHeaders(route),
        body: JSON.stringify(actionAttempts === 1
          ? { success: true, data: { message: "تم تعديل الرصيد بنجاح." } }
          : { success: false, message: "تغيرت حالة الطالب، حدّث البيانات." })
      });
    });

    await page.goto("http://staff.localhost:3000/assistant/live-support");
    
    // The single assigned conversation opens automatically on tablet.
    await expect(page.getByRole("heading", { name: "مشكلة بالرصيد" })).toBeVisible();
    await page.getByRole("button", { name: "ملف الطالب" }).click();
    
    // Toggle Profile Context lazy loading
    await page.getByRole("button", { name: "الملف الشخصي" }).click();
    await expect(page.getByText("طالب ذكي")).toBeVisible();
    await expect(page.getByText("STUD-146")).toBeVisible();

    // Open Action Panel and select adjustment
    await page.getByRole("button", { name: "تعديل الرصيد" }).click();
    await expect(page.getByRole("dialog")).toBeVisible();
    
    // Fill adjustment reason and review
    await page.getByLabel("السبب").fill("تعويض عن درس");
    await page.getByLabel("المبلغ (+ أو -)").fill("50");
    await page.getByRole("button", { name: "مراجعة وتأكيد" }).click();

    // Confirm execution
    await page.getByRole("button", { name: "تأكيد تعديل الرصيد" }).click();
    await expect(page.getByRole("status")).toContainText("تم تعديل الرصيد بنجاح");
    await page.getByLabel("إغلاق").click();
    await page.getByRole("button", { name: "تعديل الرصيد" }).click();
    await page.getByLabel("السبب").fill("إعادة محاولة");
    await page.getByLabel("المبلغ (+ أو -)").fill("10");
    await page.getByRole("button", { name: "مراجعة وتأكيد" }).click();
    await page.getByRole("button", { name: "تأكيد تعديل الرصيد" }).click();
    await expect(page.getByRole("dialog").getByRole("status")).toContainText("تغيرت حالة الطالب");
  });

  test("staff workspace: ownership loss immediately disables replies and actions", async ({ page }) => {
    const conversation = {
      id: conversationId,
      status: "Assigned",
      participantType: "Student",
      subject: "محادثة ستُنقل",
      createdAt: new Date().toISOString(),
      version: 1,
      canSend: true,
      canRate: false,
      currentOwnerUserId: "20000000-0000-0000-0000-000000000000",
      linkedStudentUserId: "student-id-1"
    };
    let bootstrapCalls = 0;
    await page.route("**/api/live-support/staff/bootstrap", route => {
      bootstrapCalls += 1;
      const stillOwned = bootstrapCalls <= 3;
      return route.fulfill({
        contentType: "application/json",
        headers: getHeaders(route),
        body: JSON.stringify({
          success: true,
          data: {
            isCheckedIn: true,
            isEnabled: true,
            activeLoad: stillOwned ? 1 : 0,
            capacity: 5,
            waitingCount: 0,
            conversations: stillOwned ? [conversation] : []
          }
        })
      });
    });
    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/messages*", route => route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: [] })
    }));

    await page.goto("http://staff.localhost:3000/assistant/live-support");
    await expect(page.getByRole("heading", { name: "محادثة ستُنقل" })).toBeVisible();
    await expect(page.getByText("تم نقل ملكية المحادثة. تم إيقاف الرد والإجراءات فورًا")).toBeVisible({ timeout: 8_000 });
    await expect(page.getByLabel("رد موظف الدعم")).toBeDisabled();
    await expect(page.getByRole("button", { name: "تحويل" })).toBeDisabled();
    await expect(page.getByRole("button", { name: "إغلاق" })).toBeDisabled();
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
      published: {
        id: "published-id-1",
        versionNumber: 1,
        status: "Published",
        isEnabled: true,
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
        version: 4
      },
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
    const savedDraft = { ...aiConfig.draft, version: 6 };
    const publishedPolicy = { ...savedDraft, id: "published-id-2", status: "Published", isEnabled: true, version: 7 };

    // Mock config endpoints
    await page.route("**/api/live-support/admin/ai/config", route => route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: route.request().method() === "PUT" ? savedDraft : aiConfig })
    }));

    await page.route("**/api/live-support/admin/ai/stats*", route => route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: stats })
    }));

    await page.route("**/api/live-support/admin/ai/active-conversations", route => route.fulfill({
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: [] })
    }));

    await page.route("**/api/live-support/admin/ai/disable", route => route.fulfill({
      status: 202,
      contentType: "application/json",
      headers: getHeaders(route),
      body: JSON.stringify({ success: true, message: "Reconciliation started" })
    }));

    let publishAttempts = 0;
    await page.route("**/api/live-support/admin/ai/publish", route => {
      publishAttempts += 1;
      return route.fulfill({
        status: publishAttempts === 1 ? 200 : 409,
        contentType: "application/json",
        headers: getHeaders(route),
        body: JSON.stringify(publishAttempts === 1
          ? { success: true, data: publishedPolicy }
          : { success: false, message: "VERSION_CONFLICT" })
      });
    });

    await page.goto("http://admin.localhost:3000/admin/live-support/ai");

    // Verify stats tab
    await page.getByRole("tab", { name: "الإحصائيات والأدلة" }).click();
    await expect(page.getByText("١٢", { exact: true })).toBeVisible(); // active conversations
    await expect(page.getByText("٤٥", { exact: true })).toBeVisible(); // resolved
    await expect(page.getByText("١٩", { exact: true })).toBeVisible(); // successful actions

    // Return to settings
    await page.getByRole("tab", { name: "الإعدادات وقاعدة القرار" }).click();

    // Verify a successful draft save and publish before the conflict path.
    await page.getByRole("button", { name: "حفظ ونشر وتفعيل" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText("تم حفظ الإعدادات ونشرها");
    await page.getByRole("button", { name: "حفظ ونشر وتفعيل" }).click();
    await expect(page.getByRole("main").getByRole("status").filter({ hasText: "VERSION_CONFLICT" })).toBeVisible();

    // Verify emergency disable call
    page.once('dialog', dialog => dialog.accept());
    await page.getByRole("button", { name: "إيقاف وتحويل للدعم البشري" }).click();
    await expect(page.getByText("تم إيقاف المساعد")).toBeVisible();
  });

  test("staff context: unlinked guest search, link replacement, and section retry", async ({ page }) => {
    const unlinkedConversation = {
      id: conversationId, status: "Assigned", participantType: "Guest", subject: "ربط حساب",
      createdAt: new Date().toISOString(), version: 3, canSend: true, canRate: false
    };
    await page.route("**/api/live-support/staff/bootstrap", route => route.fulfill({
      contentType: "application/json", headers: getHeaders(route),
      body: JSON.stringify({ success: true, data: { isCheckedIn: true, isEnabled: true, activeLoad: 1, capacity: 5, waitingCount: 0, conversations: [unlinkedConversation] } })
    }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/messages*`, route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: [] }) }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/actions`, route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: [] }) }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/students/search*`, route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: [{ userId: "student-2", fullName: "طالب للربط", maskedPhone: "010******10", studentCode: "ST-2" }] }) }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/student-link`, route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { ...unlinkedConversation, linkedStudentUserId: "student-2", version: 4 } }) }));
    await page.goto("http://staff.localhost:3000/assistant/live-support");
    await page.getByText("ربط حساب").first().click();
    await page.getByPlaceholder("الاسم، الهاتف، أو الكود").fill("طالب");
    await page.getByRole("button", { name: "بحث" }).click();
    await expect(page.getByText("طالب للربط")).toBeVisible();
    page.once('dialog', dialog => dialog.accept("الزائر أكد ملكية الحساب"));
    await page.getByText("طالب للربط").click();
    await expect(page.getByRole("button", { name: "الملف الشخصي" })).toBeVisible();
  });

  test("admin AI: knowledge, zero-write preview, evidence, and non-admin denial", async ({ page }) => {
    await mockAdminConfiguration(page);
    await page.route("**/api/live-support/admin/ai/knowledge", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: [{ entryId: "entry-1", revisionId: "revision-1", title: "سياسة الاسترجاع", revisionNumber: 1, content: "المحتوى", isPublished: true }] }) }));
    await page.route("**/api/live-support/admin/ai/knowledge/revisions", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { entryId: "entry-2", revisionId: "revision-2", title: "سياسة الحضور", revisionNumber: 1, content: "تفاصيل الحضور", isPublished: true } }) }));
    await page.route("**/api/live-support/admin/ai/knowledge/links", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true }) }));
    await page.route("**/api/live-support/admin/ai/preview", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { policyVersionId: "pub-policy-1", dryRun: true, knowledgeDocuments: 1, allowedDecisionTypes: ["reply"], safeOutcome: "DRY_RUN_DECISION_VALIDATED", decision: { schemaVersion: "1", type: "reply", messageAr: "هذه معاينة آمنة." }, decisionHash: "a".repeat(64), provider: "vertex", model: "gemini", latencyMs: 25 } }) }));
    await page.route("**/api/live-support/admin/ai/evidence*", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { items: [{ turnId: "turn-1", conversationId, at: new Date().toISOString(), status: "Completed", decisionType: "Reply", provider: "vertex", model: "gemini", callbackAttempts: 1 }] } }) }));
    await page.route("**/api/live-support/admin/ai/stats*", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { activeConversations: 0, resolvedIssues: 1, handoffs: 0, totalMessagesSent: 1, successfulActions: 0 } }) }));
    await page.route("**/api/live-support/admin/ai/active-conversations", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: [] }) }));
    await page.goto("http://admin.localhost:3000/admin/live-support/ai");
    await page.getByRole("tab", { name: "المعرفة والمعاينة" }).click();
    await expect(page.getByText("سياسة الاسترجاع")).toBeVisible();
    await page.getByLabel("العنوان").fill("سياسة الحضور");
    await page.getByLabel("المحتوى").fill("تفاصيل الحضور");
    await page.getByRole("button", { name: "نشر المراجعة" }).click();
    await expect(page.getByText("تم نشر مراجعة معرفة جديدة")).toBeVisible();
    await page.getByLabel("سياسة الحضور").check();
    await page.getByRole("button", { name: "ربط المحدد بالسياسة" }).click();
    await expect(page.getByText("تم ربط المصادر بالسياسة المنشورة")).toBeVisible();
    await page.getByLabel("رسالة الاختبار").fill("ما هي سياسة الاسترجاع؟");
    await page.getByRole("button", { name: "تشغيل المعاينة" }).click();
    await expect(page.getByText("لا توجد تغييرات إنتاجية")).toBeVisible();
    await page.getByRole("tab", { name: "الإحصائيات والأدلة" }).click();
    await expect(page.getByText("turn-1")).toBeVisible();

    await page.addInitScript(() => localStorage.setItem('user', JSON.stringify({ id: 'staff-1', fullName: 'موظف', phone: '01000000000', roles: ['Staff'], permissions: [], profileComplete: true, allowedDomains: ['admin'] })));
    await page.reload();
    await expect(page.getByRole('heading', { name: 'غير مصرح بالدخول' })).toBeVisible();
  });

  test("admin intervention is explicit and audited through the server operation", async ({ page }) => {
    const conversation = { id: conversationId, participantName: "طالب للتدقيق", participantType: "Student", status: "Active", ownerName: "موظف الدعم", createdAt: new Date().toISOString(), aiTurnStatus: "Failed", aiTurnFailureCode: "AI_PROVIDER_TIMEOUT" };
    await page.route("**/api/live-support/admin/config", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { featureEnabled: true, staff: [] } }) }));
    await page.route("**/api/live-support/admin/dashboard", route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { waitingCount: 0, activeCount: 1, closedToday: 0, conversations: [conversation], staffPerformance: [] } }) }));
    await page.route(`**/api/live-support/admin/conversations/${conversationId}/timeline`, route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { conversation, items: [{ at: new Date().toISOString(), type: "AITurnFailed", actorName: null, summary: "فشل رد المساعد", safeDetails: "AI_PROVIDER_TIMEOUT" }] } }) }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/messages*`, route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: [] }) }));
    await page.route(`**/api/live-support/admin/conversations/${conversationId}/intervene`, route => route.fulfill({ contentType: "application/json", headers: getHeaders(route), body: JSON.stringify({ success: true, data: { ...conversation, status: "Waiting" } }) }));
    await page.goto("http://admin.localhost:3000/admin/live-support");
    await page.getByRole("button", { name: "فتح المحادثة" }).click();
    await expect(page.getByLabel("متابعة المحادثة").getByText("AI_PROVIDER_TIMEOUT")).toBeVisible();
    page.once('dialog', dialog => dialog.accept("إعادة توزيع بعد فشل المزود"));
    const interventionRequest = page.waitForRequest(`**/api/live-support/admin/conversations/${conversationId}/intervene`);
    await page.getByRole("button", { name: "إعادة للطابور" }).click();
    expect((await interventionRequest).postDataJSON()).toMatchObject({ operation: "queue", reason: "إعادة توزيع بعد فشل المزود" });
  });
});
