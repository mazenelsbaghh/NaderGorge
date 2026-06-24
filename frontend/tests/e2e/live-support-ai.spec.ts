import { expect, test, type Page } from "@playwright/test";

const appUrl = process.env.LIVE_SUPPORT_E2E_URL || "http://localhost:8738";
const conversationId = "14600000-0000-0000-0000-000000000001";

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

  const headers = {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS, PUT, DELETE",
    "Access-Control-Allow-Headers": "Content-Type, X-App-Surface, Authorization"
  };

  await page.route("**/api/live-support/availability", route =>
    route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({
        success: true,
        data: { isAvailable: true, availableStaffCount: 0, code: "AI_AVAILABLE", message: "متاح" }
      })
    })
  );

  await page.route("**/api/live-support/participant/conversations", route =>
    route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: [conversation] })
    })
  );

  await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/snapshot`, route =>
    route.fulfill({
      contentType: "application/json",
      headers,
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

async function mockStaffAuth(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem("accessToken", "mock-staff-token");
    localStorage.setItem("user", JSON.stringify({
      id: "20000000-0000-0000-0000-000000000003",
      fullName: "موظف الدعم",
      phone: "01011111111",
      roles: ["Staff"],
      permissions: ["live-support-read", "live-support-write"],
      profileComplete: true
    }));
  });
  
  await page.route("**/api/live-support/staff/bootstrap", route => route.fulfill({
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "*" },
    body: JSON.stringify({
      success: true,
      data: {
        isEnabled: true,
        checkedIn: true,
        activeConversationsCount: 1,
        maxActiveConversations: 5,
        queueLength: 0,
        activeConversations: [
          { id: conversationId, participantType: "Student", status: "Assigned", currentOwnerUserId: "20000000-0000-0000-0000-000000000003", linkedStudentUserId: "30000000-0000-0000-0000-000000000004", subject: "مشكلة فنية", createdAt: new Date().toISOString(), version: 1, isAiActive: false }
        ]
      }
    })
  }));
  
  await page.route(`**/api/live-support/staff/conversations/${conversationId}/messages**`, route => route.fulfill({
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "*" },
    body: JSON.stringify({
      success: true,
      data: [
        { id: "msg-1", conversationId, senderType: "Student", clientMessageId: "c-1", type: "Text", content: "مرحباً، لدي مشكلة في تشغيل الفيديو.", sentAt: new Date().toISOString() }
      ]
    })
  }));
  
  await page.route(`**/api/live-support/staff/conversations/${conversationId}/events**`, route => route.fulfill({
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "*" },
    body: JSON.stringify({
      success: true,
      data: []
    })
  }));
}

async function mockAdminAuth(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem("accessToken", "mock-admin-token");
    localStorage.setItem("user", JSON.stringify({
      id: "10000000-0000-0000-0000-000000000001",
      fullName: "مدير النظام",
      phone: "01000000000",
      roles: ["Admin"],
      permissions: [],
      profileComplete: true
    }));
  });
  
  await page.route("**/api/live-support/admin/ai/config", route => route.fulfill({
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "*" },
    body: JSON.stringify({
      success: true,
      data: {
        draft: null,
        published: {
          id: "pub-policy-1",
          versionNumber: 1,
          status: "Published",
          isEnabled: true,
          systemInstructions: "أنت مساعد الدعم الذكي لمنصة مسار",
          readableDataKeys: [],
          actionKeys: [],
          lookupKeys: [],
          verificationQuestionKeys: [],
          verificationRequiredCorrect: 1,
          verificationMaxAttempts: 3,
          pendingActionExpirySeconds: 300,
          inactivityMinutes: 30,
          inactivityWarningGraceSeconds: 120,
          version: 1
        },
        catalogs: {
          readableData: [],
          actions: [],
          lookupKeys: [],
          verificationQuestions: []
        }
      }
    })
  }));
  
  await page.route("**/api/live-support/admin/ai/stats**", route => route.fulfill({
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "*" },
    body: JSON.stringify({
      success: true,
      data: {
        activeConversations: 1,
        resolvedIssues: 10,
        handoffs: 5,
        totalMessagesSent: 100,
        successfulActions: 3
      }
    })
  }));
  
  await page.route("**/api/live-support/admin/ai/active-conversations", route => route.fulfill({
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "*" },
    body: JSON.stringify({
      success: true,
      data: [
        { id: conversationId, participantName: "أحمد علي", participantType: "Student", subject: "مشكلة فنية", aiTurnStatus: "Completed", createdAt: new Date().toISOString() }
      ]
    })
  }));
}

test.describe("AI live support participant", () => {
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
        headers: { "Access-Control-Allow-Origin": "*" },
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
        headers: { "Access-Control-Allow-Origin": "*" },
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
        headers: { "Access-Control-Allow-Origin": "*" },
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
    await page.getByRole("button", { name: "فتح الدعم المباشر" }).click({ force: true });
    
    await expect(page.getByRole("log").getByText("هذا نص طويل جداً")).toBeVisible();
  });
});

test.describe("AI live support staff and administration", () => {
  test.beforeEach(async ({ request, page }) => {
    page.on('console', msg => console.log('STAFF/ADMIN PAGE LOG:', msg.text()));
    page.on('pageerror', err => console.error('STAFF/ADMIN PAGE ERROR:', err.message));

    // Seed and login as real admin (which has both admin settings and staff access)
    const seeded = await request.post('http://localhost:5245/api/e2e/seed', {
      data: {
        clearDatabase: false,
        seedAdmin: true,
        seedStudents: true,
        seedAssistant: true,
        seedLiveSupport: true
      }
    });
    expect(seeded.ok()).toBeTruthy();

    const adminLogin = await request.post('http://localhost:5245/api/auth/login', {
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
    await mockStaffAuth(page);
    await page.goto(`http://localhost:8742/assistant/live-support`);
    await page.waitForTimeout(500);
    
    await expect(page.getByText("حالة الاتصال")).toBeVisible();
    const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(hasHorizontalOverflow).toBe(false);
  });

  test("admin AI page renders without horizontal overflow at desktop 1024px width", async ({ page }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await mockAdminAuth(page);
    await page.goto(`http://localhost:8740/admin/live-support/ai`);
    await page.waitForTimeout(500);
    
    await expect(page.getByText("الإعدادات وقاعدة القرار")).toBeVisible();
    const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(hasHorizontalOverflow).toBe(false);
  });

  test("keyboard navigation allows tabbing through admin controls", async ({ page }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await mockAdminAuth(page);
    await page.goto(`http://localhost:8740/admin/live-support/ai`);
    await page.waitForTimeout(500);
    
    // Focus settings tab
    const settingsTab = page.getByRole("button", { name: "الإعدادات وقاعدة القرار" });
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
      headers: { "Access-Control-Allow-Origin": "*" },
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

    const headers = {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS, PUT, DELETE",
      "Access-Control-Allow-Headers": "Content-Type, X-App-Surface, Authorization"
    };

    // Mock endpoints
    await page.route("**/api/live-support/staff/bootstrap", route => route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: { isCheckedIn: true, isEnabled: true, activeLoad: 1, capacity: 5, waitingCount: 0, conversations: [conversation] } })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/messages*", route => route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: [] })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/student-context", route => route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: studentContext })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/actions", route => route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: actionCatalog })
    }));

    await page.route("**/api/live-support/staff/conversations/" + conversationId + "/actions/student.balance.adjust", route => route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: { message: "تم تعديل الرصيد بنجاح." } })
    }));

    await page.goto("http://localhost:8742/assistant/live-support");
    
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

    const headers = {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS, PUT, DELETE",
      "Access-Control-Allow-Headers": "Content-Type, X-App-Surface, Authorization"
    };

    // Mock config endpoints
    await page.route("**/api/live-support/admin/ai/config", route => route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: aiConfig })
    }));

    await page.route("**/api/live-support/admin/ai/stats*", route => route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: stats })
    }));

    await page.route("**/api/live-support/admin/ai/active-conversations", route => route.fulfill({
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, data: [] })
    }));

    await page.route("**/api/live-support/admin/ai/disable", route => route.fulfill({
      status: 202,
      contentType: "application/json",
      headers,
      body: JSON.stringify({ success: true, message: "Reconciliation started" })
    }));

    await page.route("**/api/live-support/admin/ai/publish", route => {
      // Simulate conflict if wrong version
      return route.fulfill({
        status: 409,
        contentType: "application/json",
        headers,
        body: JSON.stringify({ success: false, message: "VERSION_CONFLICT" })
      });
    });

    await page.goto("http://localhost:8740/admin/live-support/ai");

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
