# 08 — UX Direction & Sitemap

## UX Principles
- **Simple & Directed:** The student should never wonder "what do I do next?". The UI must present a clear, linear path forward.
- **Controlled, Not Oppressive:** While the system enforces strict rules (watch limits, gated progression), the UX copy and UI elements should feel supportive and motivating, not punitive.
- **Fast & Responsive:** Heavy use of skeleton loaders, optimistic UI updates, and smooth Framer Motion transitions to make the app feel native and premium.
- **High Contrast / Focus Mode:** The video player and exam interfaces must minimize distractions, utilizing a dark mode or high-contrast focus layout.

## Registration Flow (Two-Step Strategy)
To maximize initial conversion while still capturing required operational data:
- **Step 1 (Low Friction):** Student enters Student Name, Student Phone Number, Password, Grade, and Track (if applicable). *Account is created immediately. They can log in and browse.*
- **Step 2 (Operational Block):** Upon first attempting to redeem a code or access restricted content, a modal demands the remaining data: Parent Phone Number, Governorate, City/District, and School Name.

## Dashboard Design
The central hub for a logged-in student. Key modules:
- **Hero/Resume:** A prominent "Continue Learning" button taking them exactly to where they left off (the specific lesson or unfinished exam).
- **Active Packages:** Cards showing currently unlocked content with progress bars.
- **Alerts/Notifications:** Upcoming exam warnings, homework due dates, or teacher messages.
- **Quick Actions:** "Activate a Code", "View Leaderboard".

## Sitemap
```text
/
├── (Public Site)
│   ├── Home (Hero, features, testimonials)
│   ├── About Nader George
│   ├── Contact/Support
│   └── Login / Register
├── /student (Student Portal)
│   ├── Dashboard
│   ├── /packages (Browse available)
│   ├── /my-learning (Enrolled content)
│   │   └── /package/{id}
│   │       └── /lesson/{id} (The main learning environment: Video + Quiz + PDF)
│   ├── /exams
│   ├── /homework
│   ├── /profile & settings
│   └── /code-redemption
├── /teacher (Teacher Panel)
│   ├── Dashboard (Macro metrics)
│   ├── /content-builder
│   ├── /question-bank
│   └── /announcements
├── /assistant (Assistant Operations)
│   ├── /grading-queue (Essays/Homework)
│   ├── /student-search (360 view of a student)
│   └── /support-tickets
└── /admin (Admin Panel)
    ├── /finance & codes (Batch generation, tracking)
    ├── /roles & permissions
    └── /system-health
```

## Navigation Rules
- **The 3-Click Rule:** A student must be able to reach any active lesson video within a maximum of 3 clicks from their Dashboard.
- **Breadcrumbs:** Deeply nested content (e.g., Package > Section > Lesson > Exam) must display clear, clickable breadcrumbs at the top of the UI to allow rapid upward traversal.
