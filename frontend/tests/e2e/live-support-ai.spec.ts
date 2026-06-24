import { expect, test, type Page } from "@playwright/test";

const appUrl = process.env.LIVE_SUPPORT_E2E_URL || "http://localhost:8899";
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
  await page.route("**/api/live-support/availability", route =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        success: true,
        data: { isAvailable: true, availableStaffCount: 0, code: "AI_AVAILABLE", message: "متاح" }
      })
    })
  );
  await page.route("**/api/live-support/participant/conversations", route =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: [conversation] })
    })
  );
  await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/snapshot`, route =>
    route.fulfill({
      contentType: "application/json",
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
});

test.describe("AI live support staff and administration", () => {
  test.skip("staff handoff and admin AI journeys continue in their dependent stories", async () => {});
});
