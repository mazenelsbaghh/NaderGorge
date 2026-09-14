---
target: شاشات الإدارة كلها
total_score: 21
p0_count: 0
p1_count: 3
timestamp: 2026-07-23T12-15-15Z
slug: frontend-src-app-admin
---
## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|---|---:|---|
| 1 | Visibility of system status | 3/4 | Loading and errors exist, but dashboard states are uneven. |
| 2 | Match between system and real world | 3/4 | Arabic terminology is generally direct. |
| 3 | User control and freedom | 2/4 | Dense navigation and destructive flows make recovery less obvious. |
| 4 | Consistency and standards | 2/4 | The support area bypasses admin tokens and component vocabulary. |
| 5 | Error prevention | 2/4 | Native confirmation and state-heavy management forms are inconsistent. |
| 6 | Recognition rather than recall | 2/4 | 32 destinations are compressed into six expandable sidebar groups. |
| 7 | Flexibility and efficiency | 2/4 | No visible keyboard shortcuts, saved views, or bulk patterns. |
| 8 | Aesthetic and minimalist design | 2/4 | Gradients, large shadows, and rounded shapes compete with data. |
| 9 | Error recovery | 2/4 | Tables have retry, but loading, errors, and empty states are not systematic. |
| 10 | Help and documentation | 1/4 | Complex financial, HR, and support tasks lack contextual guidance. |
| **Total** | | **21/40** | **Functional but visually inconsistent** |

## Anti-patterns verdict

The shell has a considered Arabic-first foundation, but the collection reads like several admin products combined. `AdminShellChrome` is branded and tokenized; `AdminLiveSupportPageClient` introduces an unrelated Slate/Cyan system. The shared stat card adds decorative gradients, oversized icons, 2xl shadows, blur, and lift animation to operational metrics. This causes the data hierarchy to compete with component styling.

The deterministic scan reported 9 warnings: one side-tab border in `content/exams/[id]/ExamProfilePageClient.tsx:485`, and eight gray-on-colored-background findings in `live-support/AdminLiveSupportPageClient.tsx:68-89`. The side-tab is a weak false-positive because it is a 2px neutral quotation border, but the support-page color findings confirm a real token drift.

## Overall impression

The underlying admin system is usable and has a good RTL foundation, yet it needs consolidation more than new visual decoration. The single largest opportunity is to enforce one compact component and semantic-color system across every domain.

## What's working

- Shared shell gives RTL direction, page title, breadcrumbs, permission-aware navigation, theme controls, focus styling, and a mobile fallback.
- `AdminDataTable` provides loading, retry, empty, pagination, expandable rows, and labelled row actions as a reusable baseline.
- The navy, teal, off-white brand tokens are appropriate for a serious educational operations product.

## Priority issues

### [P1] One admin shell, two visual languages

`AdminLiveSupportPageClient` uses direct Slate/Cyan/Emerald/Red utilities and 24px/3xl surfaces while shared admin components rely on `--admin-*` tokens. This breaks visual continuity and makes future theming costly. Move live-support components to semantic admin tokens, shared buttons, status badges, tables, and form controls. Suggested command: `$impeccable polish`.

### [P1] Navigation discovery relies on hover and memory

Desktop navigation starts at 80px and reveals its 32 destinations only on hover. Groups are not searchable, only one active group opens by default, and the mobile “more” sheet turns a large operations map into a long grid. Keep the sidebar expanded or offer a persistent collapse control, add a command/search entry, and use task-oriented group labels with recent/favorite destinations. Suggested command: `$impeccable layout`.

### [P1] Decorative treatment is stronger than information hierarchy

`AdminStatCard` uses full gradient surfaces, 48px ghost icons, blur, large shadows, hover translation, and display-sized numerals. Across 89 admin files with gradient/shadow/large-radius styling this becomes a visual tax on dense operations work. Reduce KPI cards to one restrained variant, reserve teal for selected or progress state, and use gold only for achievement or warning. Suggested command: `$impeccable quieter`.

### [P2] Tables meet desktop needs but do not adapt into a usable compact workflow

`AdminDataTable` imposes a 780px minimum, while live support uses 980px. Horizontal scrolling is defensible for desktop, but critical columns, filters, and actions have no responsive priority pattern. Use responsive column visibility, an explicit detail drawer, sticky primary identity/action cells, and filter summaries. Suggested command: `$impeccable adapt`.

### [P2] High-stakes tasks lack progressive guidance and recovery structure

Finance, payroll, HR, and live-support management expose many filters, states, and destructive actions. Existing `confirm()` and toast feedback are not enough for irreversible locks and approvals. Add action summaries, preflight validation, reason requirements, inline consequences, undo where possible, and consistent empty/error/skeleton states. Suggested command: `$impeccable harden`.

## Persona red flags

- **مدير عمليات خبير:** للوصول إلى صفحة مثل payroll أو reports عليه تذكّر المجموعة الصحيحة ثم المرور على شريط أيقونات قابل للتمدد. لا توجد اختصارات أو وصول سريع للصفحات المتكررة، فيتكرر الاحتكاك يوميًا.
- **مدير جديد:** عند فتح صفحة الإدارة يرى قائمة أدوات طويلة متساوية الوزن. لا توجد بداية موجهة حسب الدور أو شرح لاختلاف “العمليات” و“الموارد البشرية” و“المالية والرواتب”، ما يرفع عبء القرار قبل البدء.
- **مشرف دعم مباشر:** ينتقل بين نظام ألوان ومقاييس دعم مستقلة وشل الإدارة العام. لا يتعلم مفردات تحكم واحدة، ويميل إلى التردد في شاشات السعة والجداول والردود الثابتة.

## Minor observations

- `tracking-[0.22em]` في عنوان القسم العربي يضعف الإيقاع الطباعي ويبدو كلابل إنجليزي مترجم.
- بطاقات التنقل في شريط الهاتف تستخدم نصًا صغيرًا مع خمسة عناصر ثابتة، وقد تقص التسميات العربية الطويلة.
- حالة التحميل في الدعم Spinner منفرد، بينما الجدول المشترك يملك skeleton أوضح؛ وحّد السلوك.
- لون التحذير في صفحات HR وsupport يستخدم تسميات وألوانًا غير موحدة؛ اربطه برموز semantic ثابتة.

## Questions to consider

- هل تريد أن تبدأ لوحة الإدارة بمسارات عمل حسب الدور، أم تظل فهرسًا كاملًا للأدوات؟
- هل ينبغي أن تكون بطاقات KPI هادئة ومعلوماتية، أم تحتفظ ببطاقة واحدة بارزة فقط لأولوية اليوم؟
- ما الذي يجب أن يظهر دائمًا على الهاتف: ثلاث أدوات ثابتة، أم اختصارات يحددها كل مستخدم؟
