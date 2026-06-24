# خطة العمل الشاملة: نظام متابعة أولياء الأمور والتطبيقات الذكية
# Comprehensive Implementation Plan: Parent Tracking System & Mobile Apps

هذه هي الخطة الشاملة والتفصيلية لتنفيذ نظام متابعة أولياء الأمور (رقم المتابعة) متضمناً تغييرات قاعدة البيانات، الـ APIs، تعديلات واجهة الطالب على الويب، الهيكل البرمجي لتطبيقات الموبايل (Kotlin لأندرويد و Swift لآيفون)، ونظام الإشعارات اللحظية عبر Firebase.

---

## 1. تصميم قاعدة البيانات والتهجير (Database Schema & Migrations)

سنقوم بإضافة الحقول والجداول الجديدة إلى قاعدة بيانات PostgreSQL عبر C# EF Core Migrations.

### تعديل جدول `StudentProfile` الحالي:
سنضيف حقلين جديدين:
1. `ParentTrackingCode` (سلسلة نصية فريدة من 6 أحرف): رمز فريد يُنشأ تلقائياً لكل طالب عند التسجيل.
2. `HasSeenTrackingCodePopup` (قيمة منطقية Boolean، افتراضياً `false`): لتحديد ما إذا كان الطالب قد شاهد النافذة المنبثقة الترحيبية وتخطيها.

```csharp
// Path: backend/src/NaderGorge.Domain/Entities/StudentProfile.cs
public class StudentProfile : BaseEntity
{
    // ... الحقول الحالية ...

    // رقم المتابعة لولي الأمر (فريد ومفهرس للبحث السريع)
    public string? ParentTrackingCode { get; set; }
    
    // هل رأى الطالب البوب اب الترحيبي الخاص برقم المتابعة؟
    public bool HasSeenTrackingCodePopup { get; set; } = false;
}
```

### إنشاء جدول `ParentDeviceToken` الجديد:
جدول لتخزين رموز أجهزة الهواتف الخاصة بأولياء الأمور لإرسال الإشعارات اللحظية.

```csharp
// Path: backend/src/NaderGorge.Domain/Entities/Notifications/ParentDeviceToken.cs
using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.Notifications;

public class ParentDeviceToken : BaseEntity
{
    public Guid StudentId { get; set; }
    public StudentProfile Student { get; set; } = null!;
    
    public string DeviceToken { get; set; } = string.Empty; // رمز جهاز FCM
    public string Platform { get; set; } = string.Empty;    // "android" أو "ios"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## 2. تطوير خادم الـ Backend والـ APIs (C# .NET 9)

### إضافة سياسة الصلاحيات (Authorization Policy) لولي الأمر:
في ملف `Program.cs` لضمان حماية بيانات الطلاب وجعل الوصول مقتصر على أولياء الأمور الموثقين.
```csharp
// Path: backend/src/NaderGorge.API/Program.cs
builder.Services.AddAuthorization(options =>
{
    // السياسات الحالية ...
    options.AddPolicy("RequireParent", policy => policy.RequireRole("Parent"));
});
```

### توليد التوكن الخاص بولي الأمر (Token Generation):
سنضيف ميثود جديدة في `TokenService` لتوليد توكن خفيف لولي الأمر يحتوي على معرف الطالب `StudentId` وصلاحية `Parent`.
```csharp
// Path: backend/src/NaderGorge.Infrastructure/Services/TokenService.cs
public string GenerateParentToken(Guid studentId)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.Role, "Parent"),
        new Claim("StudentId", studentId.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:Secret"]!));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = DateTime.UtcNow.AddYears(1), // توكن طويل الأجل لولي الأمر لمنع تسجيل الخروج المتكرر
        SigningCredentials = creds,
        Issuer = _config["JwtSettings:Issuer"],
        Audience = _config["JwtSettings:Audience"]
    };

    var tokenHandler = new JwtSecurityTokenHandler();
    return tokenHandler.CreateEncodedJwt(token);
}
```

### نقاط الاتصال البرمجية (Endpoints):

#### أ) نقاط خاصة بالطالب (Student Web Client):
* **تعديل الـ Bootstrap** (`GET /api/student/shell-bootstrap`):
  يُرجع رمز المتابعة وحالة البوب اب:
  ```json
  {
    "unreadNotificationsCount": 3,
    "currentBalance": 150.0,
    "parentTrackingCode": "NG79F4",
    "hasSeenTrackingCodePopup": false
  }
  ```
* **تأكيد مشاهدة البوب اب** (`POST /api/student/acknowledge-tracking-popup`):
  تغيير حالة `HasSeenTrackingCodePopup` إلى `true` في قاعدة البيانات.

#### ب) نقاط خاصة بتطبيق ولي الأمر (Parent Mobile App) - `ParentController.cs`:
* **ربط الكود وتفعيل المتابعة** (`POST /api/parent/verify-code`):
  - المدخلات: `{ "trackingCode": "NG79F4", "deviceToken": "FCM_TOKEN_HERE", "platform": "android" }`
  - المخرجات: `{ "token": "PARENT_JWT_TOKEN", "studentName": "أحمد محمد" }`
  - المنطق: يتحقق من صحة الكود، ويسجل رمز الـ FCM في جدول `ParentDeviceToken` لربطه بالطالب، ثم يرجع التوكن.
  
* **تفاصيل الطالب الشاملة** (`GET /api/parent/student-details`):
  - يتطلب صلاحية `RequireParent`.
  - المخرجات: بيانات الطالب بالتفصيل الممل:
    ```json
    {
      "studentName": "أحمد محمد",
      "grade": "الصف الثالث الثانوي",
      "school": "مدرسة الأورمان الثانوية",
      "avatarSlug": "avatar-lion",
      "attendance": {
        "totalLessons": 20,
        "watchedLessons": 18,
        "completionRate": 90.0
      },
      "exams": [
        {
          "examTitle": "اختبار الكيمياء العضوية الشامل",
          "score": 45,
          "totalScore": 50,
          "percentage": 90.0,
          "submittedAt": "2026-06-20T12:00:00Z",
          "status": "Passed"
        }
      ],
      "homeworks": [
        {
          "title": "واجب المحاضرة الخامسة كيمياء",
          "isSubmitted": true,
          "grade": "A",
          "submittedAt": "2026-06-22"
        }
      ],
      "warnings": [
        {
          "reason": "عدم حضور المحاضرة المباشرة وتخطي الوقت المحدد للمشاهدة",
          "severity": "High",
          "createdAt": "2026-06-23T08:30:00Z"
        }
      ]
    }
    ```

---

## 3. تعديل واجهة الطالب على الويب (Next.js 16 / React 19)

### النافذة المنبثقة الترحيبية (One-time Popup):
في مكون `StudentShellChrome.tsx` سنضيف فحصاً إذا كان الطالب لم يرَ الكود بعد (`hasSeenTrackingCodePopup === false`).
* **التصميم**: نافذة عائمة شفافة (Glassmorphism) مع خلفية مظلمة، تظهر بتأثير حركي سلس (Framer Motion).
* **المحتوى باللغة العربية**:
  - عنوان جذاب: "تابع مستواك الدراسي مع ولي أمرك"
  - رمز المتابعة الخاص بالطالب مكتوباً بخط عريض وواضح جداً مع زر "نسخ الرمز" (Copy to Clipboard).
  - رسالة توضيحية: "يرجى نسخ هذا الرمز وإعطائه لولي أمرك ليتمكن من ربطه في تطبيق ولي الأمر ومتابعة درجاتك وتقارير حضورك لحظة بلحظة."
  - زر إغلاق "حفظ ومتابعة" يقوم بإرسال طلب الـ Acknowledge للـ API لمنع ظهوره مجدداً.

```tsx
// زر النسخ والتفاعل مع الـ API
const copyCodeToClipboard = () => {
  navigator.clipboard.writeText(parentTrackingCode);
  toast.success("تم نسخ رمز المتابعة بنجاح!");
};
```

### شارة الهيدر الدائمة (Header Badge):
سنقوم بوضع pill صغير في الهيدر بجوار زر التنبيهات:
* الشكل: `رمز المتابعة: NG79F4 [أيقونة النسخ]`
* متاح دائماً للطالب في حال رغب في إعطائه لولي أمره لاحقاً.

---

## 4. تطبيق أندرويد لولي الأمر (Kotlin - Jetpack Compose)

سيكون التطبيق مبنياً بأحدث تقنيات أندرويد ليعطي مظهراً غاية في الاحترافية والجمال.

### هيكل الكود المقترح (App Architecture):
```text
com.nadergorge.parent/
│
├── data/
│   ├── api/          # Retrofit endpoints (ParentApiService)
│   ├── model/        # StudentDetailsResponse, VerifyCodeRequest
│   └── repository/   # ParentRepository
│
├── ui/
│   ├── screens/
│   │   ├── LinkingScreen.kt      # شاشة إدخال الكود والربط
│   │   ├── DashboardScreen.kt    # الشاشة الرئيسية والتبويبات
│   │   └── detail/
│   │       ├── OverviewTab.kt    # النظرة العامة والملخص
│   │       ├── VideoProgressTab.kt # تقارير حضور الفيديوهات
│   │       └── ExamsGradesTab.kt # درجات الامتحانات والواجبات
│   └── theme/
│       ├── Theme.kt  # نظام الألوان المتناسق والخطوط
│       └── Color.kt
│
└── service/
    └── ParentFirebaseMessagingService.kt  # استقبال الإشعارات اللحظية
```

### شاشة الربط (LinkingScreen):
* شاشة ترحيبية نظيفة، حقل إدخال الكود (6 خانات).
* عند الضغط على "ربط الطالب"، يظهر لودر حركي أنيق، ثم تظهر رسالة نجاح مع اسم الطالب.
* يتم تخزين التوكن المستلم في الـ `EncryptedSharedPreferences` للأمان.

### شاشة عرض البيانات الرئيسية (DashboardScreen):
* استخدام بطاقات (Cards) منحنية وخطوط حديثة (مثل Cairo أو Tajawal).
* عرض رسوم بيانية دائرية (Circular Progress Bars) لنسب الحضور والمشاهدة.
* استخدام قوائم سريعة الاستجابة لعرض درجات الطالب (اللون الأخضر للناجح، والأحمر للتنبيهات أو الفشل).

---

## 5. تطبيق آيفون لولي الأمر (Swift - SwiftUI) - نظام Liquid Glass الفاخر

تطبيق iOS مخصص يتبع معايير أبل للتصميم الفاخر (iOS Design Guidelines) ومتبوعاً بنظام تصميم **Liquid Glass (الزجاج السائل)** ليعطي تجربة بصرية استثنائية (Premium UI/UX).

### مبادئ تصميم Liquid Glass في التطبيق:
1. **الخلفيات السائلة المتحركة (Fluid Liquid Backgrounds)**:
   - استخدام ألوان متدرجة متحركة وسلسة (Moving Radial/Mesh Gradients) تتحرك ببطء في الخلفية باستخدام `TimelineView` أو `Canvas` لخلق إحساس بالحياة والعمق البصري.
2. **البطاقات الزجاجية (Glassmorphic Frosted Cards)**:
   - تصميم الكروت والبطاقات الأكاديمية باستخدام خامات الشفافية الضبابية لـ Apple (`.background(.ultraThinMaterial)` أو `.background(.thinMaterial)`).
   - تفعيل الـ Shadow والـ Ambient glow خلف الكروت لتعزيز البعد الثالث.
3. **الحواف اللامعة (Glossy Stroked Borders)**:
   - إضافة إطار دقيق جداً (Border) بسمك `0.5pt` أو `1pt` بلون متدرج شفاف (`LinearGradient` من اللون الأبيض الشفاف إلى شفاف تماماً) لمحاكاة انعكاس الضوء على حواف الزجاج.
4. **العناصر الحركية والتفاعل (Micro-interactions)**:
   - استخدام اهتزازات خفيفة استجابة للمس (Haptic Feedback).
   - استخدام الـ Spring Animations التلقائية من SwiftUI (`.animation(.spring(response: 0.4, dampingFraction: 0.7), value: ...)`).

### هيكل الكود المقترح (App Architecture):
```text
NaderGorgeParent/
│
├── Models/
│   └── StudentModels.swift
│
├── Services/
│   ├── APIService.swift
│   └── NotificationManager.swift
│
├── Views/
│   ├── Components/
│   │   ├── LiquidBackgroundView.swift # الخلفية السائلة المتحركة
│   │   ├── GlassCardModifier.swift   # تأثير الزجاج السائل اللامع
│   │   └── CircularProgressView.swift # مؤشر المشاهدة الزجاجي
│   │
│   ├── LinkingView.swift        # واجهة ربط كود الطالب
│   ├── DashboardView.swift      # لوحة التحكم الرئيسية بالتبويبات
│   ├── OverviewTabView.swift    # عرض ملخص حالة الطالب
│   ├── LecturesTabView.swift    # عرض الفيديوهات المكتملة
│   └── GradesTabView.swift      # درجات الاختبارات والتنبيهات
│
└── SupportingFiles/
    ├── Assets.xcassets
    └── Info.plist
```

### تطبيق نظام Liquid Glass برمجياً في SwiftUI:
سنقوم بتعريف `ViewModifier` خاص بالزجاج السائل ليتم تطبيقه على أي كارت في التطبيق بسهولة:
```swift
struct LiquidGlassCardModifier: ViewModifier {
    func body(content: Content) -> some View {
        content
            .padding()
            .background(.ultraThinMaterial) // زجاج ضبابي
            .cornerRadius(20)
            .overlay(
                RoundedRectangle(cornerRadius: 20)
                    .stroke(
                        LinearGradient(
                            colors: [.white.opacity(0.4), .white.opacity(0.05), .black.opacity(0.1)],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ),
                        lineWidth: 1
                    ) // حافة لامعة تحاكي انعكاس الضوء
            )
            .shadow(color: Color.black.opacity(0.15), radius: 10, x: 0, y: 10)
    }
}

extension View {
    func liquidGlassCard() -> some View {
        modifier(LiquidGlassCardModifier())
    }
}
```

---

## 6. نظام الإشعارات اللحظية والـ Firebase (Push Notifications)

### أ) إعداد مشروع Firebase Console:
1. الدخول بـ Google Account إلى [Firebase Console](https://console.firebase.google.com).
2. إنشاء مشروع باسم `Nader Gorge Dashboard`.
3. إضافة تطبيق Android (برمز الحزمة مثلاً `com.nadergorge.parent`) وتنزيل ملف `google-services.json`.
4. إضافة تطبيق iOS (برمز الباقة `com.nadergorge.parent.ios`) وتنزيل ملف `GoogleService-Info.plist`.
5. توليد ملف المفتاح الخاص بخادم المطورين (Service Account JSON) من إعدادات المشروع لتخزينه بالخادم.

### ب) خادم الخلفية (BullMQ Worker) - Node.js:
عندما يقوم الطالب بأي نشاط هام (تسليم واجب، حل امتحان، الحصول على تحذير)، يقوم سيرفر الـ .NET بإدراج مهمة إرسال إشعار في الطابور (BullMQ Queue).
يقوم الـ Worker بالتقاط المهمة والبحث عن رموز الأجهزة المرتبطة بالطالب وإرسال الإشعار فورياً:

```typescript
// Path: worker/src/jobs/notification-sender.ts
import admin from 'firebase-admin';

// تهيئة Firebase Admin SDK مرة واحدة
if (admin.apps.length === 0) {
  admin.initializeApp({
    credential: admin.credential.cert(JSON.parse(process.env.FIREBASE_SERVICE_ACCOUNT_JSON!))
  });
}

export async function processParentPushNotification(studentId: string, title: string, body: string, category: string) {
  // 1. جلب التوكنات الخاصة بولي أمر الطالب من قاعدة البيانات
  const res = await pool.query(
    'SELECT "DeviceToken" FROM "ParentDeviceTokens" WHERE "StudentId" = $1',
    [studentId]
  );
  
  const tokens = res.rows.map(row => row.DeviceToken);
  
  if (tokens.length === 0) {
    console.log(`No parent devices linked for student ${studentId}. Skipping push.`);
    return;
  }

  // 2. إرسال الإشعارات لكل الأجهزة المرتبطة بولي الأمر
  const message = {
    notification: { title, body },
    tokens: tokens,
    data: {
      studentId: studentId,
      category: category
    }
  };

  const response = await admin.messaging().sendEachForMulticast(message);
  console.log(`Successfully sent ${response.successCount} push notifications to parents.`);
}
```

---

## خطة التحقق والضمان الأمني (Verification & Security Plan)

### الاختبارات التلقائية (Automated Testing):
1. **خلفية الخادم (C#)**: اختبارات وحدة للتحقق من أن رقم المتابعة يُنشأ بشكل عشوائي فريد لكل طالب، ولا يمكن استخدامه مرتين للربط.
2. **التحقق من الصلاحيات**: اختبار أن endpoints الـ `/api/parent/student-details` لا تسمح بأي وصول إلا بالتوكن الذي يحمل ادعاء (Claim) الطالب المناسب.

### الاختبارات اليدوية البصرية (Manual & Visual Verification):
1. تسجيل دخول طالب جديد والتأكد من ظهور البوب اب الأنيق ذو الخلفية الشفافة مرة واحدة فقط وحفظ حالته.
2. استخدام كود الطالب على تطبيق ولي الأمر (الأندرويد والآيفون) والتأكد من الربط الفوري.
3. التلاعب بسير دراسات الطالب (حل اختبار مثلاً) ورصد وصول الإشعار اللحظي فوراً لهاتف ولي الأمر وتحديث البيانات داخل التطبيق تلقائياً.
