import Foundation
import SwiftUI

@MainActor
public struct DashboardView: View {
    @StateObject private var viewModel: DashboardViewModel
    @State private var selectedTab: Tab = .home
    @State private var activeSubScreen: String? = nil // "profile", "attendance", "notes", "fees", "notifications", "settings"
    @State private var showStudentSelector = false
    @State private var selectedTeacherId: String?
    @State private var selectedPackageId: String?
    @State private var selectedTermId: String?
    @State private var homeworkFilter: HomeworkFilter = .all
    @State private var notificationsEnabled = false
    @Environment(\.colorScheme) var colorScheme
    
    public var onAddStudent: () -> Void
    
    public enum Tab: String, CaseIterable {
        case home = "الرئيسية"
        case schedule = "المشاهدات"
        case homework = "الواجبات"
        case grades = "امتحانات"
        case more = "المزيد"
    }

    private enum HomeworkFilter: String, CaseIterable {
        case all = "الكل"
        case pending = "متبقي"
        case submitted = "تم التسليم"
    }
    
    private var isDark: Bool {
        colorScheme == .dark
    }
    
    public init(viewModel: DashboardViewModel? = nil, onAddStudent: @escaping () -> Void) {
        self._viewModel = StateObject(wrappedValue: viewModel ?? DashboardViewModel())
        self.onAddStudent = onAddStudent
    }
    
    public var body: some View {
        ZStack {
            // Background color
            if isDark {
                BrandColors.darkBackground
                    .ignoresSafeArea()
            } else {
                BrandColors.offWhite
                    .ignoresSafeArea()
            }
            
            // Screen router
            if let subScreen = activeSubScreen {
                subScreenRouter(subScreen: subScreen)
            } else {
                mainScaffold
            }
        }
        .environment(\.layoutDirection, .rightToLeft)
        .onAppear {
            Task {
                await viewModel.fetchDetails()
            }
        }
        .task(id: viewModel.selectedProfile?.studentId) {
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(30))
                guard !Task.isCancelled else { return }
                await viewModel.refreshActiveStudent()
            }
        }
    }
    
    // --- Main Dashboard Scaffold (Tabs + Content) ---
    private var mainScaffold: some View {
        ZStack(alignment: .bottom) {
            VStack(spacing: 0) {
                // Header Top Bar
                headerTopBar
                    .padding()
                    .background(isDark ? BrandColors.darkCard : .white)
                
                // Main Content Area
                if viewModel.isLoading {
                    Spacer()
                    ProgressView()
                        .tint(BrandColors.teal)
                        .scaleEffect(1.5)
                    Spacer()
                } else if let details = viewModel.studentDetails {
                    ScrollView {
                        VStack(spacing: 16) {
                            switch selectedTab {
                            case .home:
                                homeTab(details: details)
                            case .schedule:
                                scheduleTab(details: details)
                            case .homework:
                                homeworkTab(details: details)
                            case .grades:
                                gradesTab(details: details)
                            case .more:
                                moreTab()
                            }
                            
                            // Safe padding to scroll past the floating bottom bar
                            Spacer()
                                .frame(height: 100)
                        }
                        .padding()
                    }
                } else {
                    Spacer()
                    VStack(spacing: 16) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .font(.system(size: 40))
                            .foregroundColor(BrandColors.warmGold)
                        Text("لم يتم العثور على بيانات الطالب.")
                            .font(.custom("Tajawal-Bold", size: 15))
                            .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                        Button("إعادة المحاولة") {
                            Task {
                                await viewModel.fetchDetails()
                            }
                        }
                        .font(.custom("Tajawal-Bold", size: 14))
                        .padding(.horizontal, 24)
                        .padding(.vertical, 10)
                        .background(BrandColors.teal)
                        .foregroundColor(.white)
                        .cornerRadius(10)
                    }
                    Spacer()
                }
            }
            
            // Bottom Navigation Bar
            bottomTabBar
        }
    }
    
    private var headerTopBar: some View {
        HStack {
            MassarLogoView(isDarkBg: isDark, showText: false)
            
            Spacer()
            
            // Student switcher
            Button(action: { showStudentSelector.toggle() }) {
                HStack(spacing: 4) {
                    Text(viewModel.selectedProfile?.name ?? "منصة مسار")
                        .font(.custom("Tajawal-Bold", size: 14))
                        .fontWeight(.black)
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                    
                    Image(systemName: "chevron.down")
                        .font(.system(size: 11, weight: .bold))
                        .foregroundColor(BrandColors.teal)
                }
            }
            .sheet(isPresented: $showStudentSelector) {
                studentSelectorSheet
            }
            
            Spacer()
            
            // Notification bell
            Button(action: { activeSubScreen = "notifications" }) {
                Image(systemName: "bell.fill")
                    .foregroundColor(BrandColors.teal)
                    .font(.system(size: 18))
            }
        }
    }
    
    // Bottom Tab View (Redesigned as Floating Glassmorphic Nav Bar)
    private var bottomTabBar: some View {
        HStack(spacing: 0) {
            tabItem(tab: .home, iconName: "house", activeIconName: "house.fill")
            tabItem(tab: .schedule, iconName: "play", activeIconName: "play.fill")
            tabItem(tab: .homework, iconName: "pencil", activeIconName: "pencil")
            tabItem(tab: .grades, iconName: "star", activeIconName: "star.fill")
            tabItem(tab: .more, iconName: "line.3.horizontal", activeIconName: "line.3.horizontal")
        }
        .padding(.vertical, 8)
        .background(
            RoundedRectangle(cornerRadius: 24)
                .fill(isDark ? BrandColors.darkCard.opacity(0.85) : Color.white.opacity(0.85))
                .shadow(color: Color.black.opacity(isDark ? 0.4 : 0.1), radius: 16, x: 0, y: 8)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 24)
                .stroke(isDark ? Color.white.opacity(0.08) : Color.black.opacity(0.05), lineWidth: 1)
        )
        .padding(.horizontal, 16)
        .padding(.bottom, 16)
    }
    
    private func tabItem(tab: Tab, iconName: String, activeIconName: String) -> some View {
        Button(action: {
            withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                selectedTab = tab
            }
        }) {
            VStack(spacing: 4) {
                // Top Indicator Line
                ZStack {
                    Capsule()
                        .fill(selectedTab == tab ? BrandColors.teal : Color.clear)
                        .frame(width: 32, height: 3)
                }
                .frame(height: 3)
                .padding(.bottom, 4)
                
                Image(systemName: selectedTab == tab ? activeIconName : iconName)
                    .font(.system(size: 20, weight: selectedTab == tab ? .semibold : .regular))
                    .foregroundColor(selectedTab == tab ? BrandColors.teal : .gray)
                
                Text(tab.rawValue)
                    .font(.custom("Tajawal-Medium", size: 10))
                    .foregroundColor(selectedTab == tab ? BrandColors.teal : .gray)
            }
            .frame(maxWidth: .infinity)
        }
    }
    
    // Student Selector Sheet
    private var studentSelectorSheet: some View {
        VStack(spacing: 20) {
            Text("الطلاب المرتبطين")
                .font(.custom("Tajawal-Bold", size: 18))
                .fontWeight(.black)
                .padding(.top)
            
            List {
                ForEach(viewModel.linkedProfiles, id: \.studentId) { profile in
                    HStack {
                        Text(profile.name)
                            .font(.custom("Tajawal-Bold", size: 15))
                        Spacer()
                        if profile.studentId == viewModel.selectedProfile?.studentId {
                            Image(systemName: "checkmark")
                                .foregroundColor(BrandColors.teal)
                        }
                    }
                    .contentShape(Rectangle())
                    .onTapGesture {
                        viewModel.switchProfile(profile)
                        showStudentSelector = false
                    }
                }
                
                Button(action: {
                    showStudentSelector = false
                    onAddStudent()
                }) {
                    HStack {
                        Image(systemName: "plus.circle.fill")
                        Text("ربط طالب جديد")
                    }
                    .font(.custom("Tajawal-Bold", size: 15))
                    .foregroundColor(BrandColors.teal)
                }
            }
        }
        .environment(\.layoutDirection, .rightToLeft)
    }
    
    // --- Sub-Screen Router (Full pages) ---
    @ViewBuilder
    private func subScreenRouter(subScreen: String) -> some View {
        let details = viewModel.studentDetails
        let name = details?.studentName ?? viewModel.selectedProfile?.name ?? "طالب مسار"
        let grade = details?.grade ?? "الصف الدراسي"
        let school = details?.school ?? "مدرسة مسار"
        
        switch subScreen {
        case "profile":
            profileView(name: name, grade: grade, school: school)
        case "attendance":
            attendanceView(
                watched: details?.attendance.watchedLessons ?? 0,
                total: details?.attendance.totalLessons ?? 0,
                rate: details?.attendance.completionRate ?? 0
            )
        case "courses":
            coursesView(details?.courses ?? [])
        case "notes":
            teacherNotesView()
        case "fees":
            feesView()
        case "notifications":
            notificationsView()
        case "settings":
            settingsView()
        default:
            EmptyView()
        }
    }
    
    // --- Tabs Implementation ---
    
    // Tab 1: Home View (Dashboard metrics grid)
    private func homeTab(details: StudentDetailsResponse) -> some View {
        let latestWarnings = details.warnings.sorted { $0.createdAt > $1.createdAt }.prefix(2)
        return VStack(spacing: 16) {
            // Welcome card
            HStack(spacing: 16) {
                Circle()
                    .fill(BrandColors.deepNavy)
                    .frame(width: 56, height: 56)
                    .overlay(
                        Text(String(details.studentName.prefix(1)))
                            .font(.custom("Tajawal-Bold", size: 22))
                            .foregroundColor(.white)
                    )
                
                VStack(alignment: .leading, spacing: 2) {
                    Text("مرحباً ولي أمر")
                        .font(.custom("Tajawal-Regular", size: 12))
                        .foregroundColor(.gray)
                    Text(details.studentName)
                        .font(.custom("Tajawal-Bold", size: 16))
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                    Text("\(details.grade) • \(details.school)")
                        .font(.custom("Tajawal-Regular", size: 11))
                        .foregroundColor(.gray)
                }
                Spacer()
            }
            .padding()
            .background(isDark ? BrandColors.darkCard : .white)
            .cornerRadius(16)
            .shadow(color: Color.black.opacity(isDark ? 0.3 : 0.05), radius: 8, x: 0, y: 4)
            
            // Grid Metrics
            VStack(spacing: 12) {
                HStack(spacing: 12) {
                    metricWidgetCard(title: "المشاهدات", value: "\(details.attendance.completionRate.toInt())%", sub: "نسبة إكمال الحصص", icon: "checkmark.circle.fill", color: BrandColors.teal) {
                        activeSubScreen = "attendance"
                    }
                    metricWidgetCard(title: "امتحانات", value: "\(details.exams.count)", sub: "محاولات مسجلة", icon: "star.fill", color: BrandColors.warmGold) {
                        selectedTab = .grades
                    }
                }
                HStack(spacing: 12) {
                    metricWidgetCard(title: "الواجبات", value: "\(details.homeworks.filter { !$0.isSubmitted }.count)", sub: "واجبات متبقية", icon: "pencil.and.outline", color: BrandColors.deepNavy) {
                        selectedTab = .homework
                    }
                    metricWidgetCard(title: "الكورسات", value: "\(details.courses.count)", sub: "كورس مشتري", icon: "list.bullet", color: BrandColors.warmGold) {
                        activeSubScreen = "courses"
                    }
                }
            }
            
            if let warning = latestWarnings.first {
                HStack(spacing: 12) {
                    Image(systemName: "exclamationmark.triangle.fill")
                        .foregroundColor(BrandColors.warmGold)
                        .font(.system(size: 18))
                        .padding(8)
                        .background(BrandColors.warmGold.opacity(0.15))
                        .clipShape(Circle())
                    VStack(alignment: .leading, spacing: 2) {
                        Text("تنبيه مهم")
                            .font(.custom("Tajawal-Bold", size: 13))
                            .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                        Text(warning.reason)
                            .font(.custom("Tajawal-Regular", size: 12))
                            .foregroundColor(.gray)
                    }
                    Spacer()
                }
                .padding()
                .background(BrandColors.warmGold.opacity(isDark ? 0.08 : 0.12))
                .cornerRadius(12)
            }
            
            // Latest Notifications Section ("آخر التنبيهات")
            VStack(spacing: 12) {
                HStack {
                    Text("آخر التنبيهات")
                        .font(.custom("Tajawal-Bold", size: 16))
                        .fontWeight(.black)
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                    
                    Spacer()
                    
                    Button(action: { activeSubScreen = "notifications" }) {
                        Text("عرض الكل")
                            .font(.custom("Tajawal-Bold", size: 12))
                            .foregroundColor(BrandColors.teal)
                    }
                }
                .padding(.top, 8)
                
                if latestWarnings.isEmpty {
                    Text("لا توجد تنبيهات مسجلة حتى الآن.")
                        .font(.custom("Tajawal-Regular", size: 13))
                        .foregroundColor(.gray)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding()
                        .background(isDark ? BrandColors.darkCard : .white)
                        .cornerRadius(12)
                } else {
                    ForEach(Array(latestWarnings), id: \.id) { warning in
                        warningSummaryRow(warning)
                    }
                }
            }
            
            // General Academic Progress Visual Card
            VStack(alignment: .leading, spacing: 12) {
                Text("التقدم الأكاديمي العام")
                    .font(.custom("Tajawal-Medium", size: 12))
                    .foregroundColor(.white.opacity(0.8))
                
                Text("مستوى إكمال الحصص")
                    .font(.custom("Tajawal-Bold", size: 18))
                    .fontWeight(.black)
                    .foregroundColor(.white)
                
                // Progress Bar
                VStack(alignment: .leading, spacing: 6) {
                    GeometryReader { geometry in
                        ZStack(alignment: .trailing) {
                            Capsule()
                                .fill(Color.white.opacity(0.2))
                                .frame(height: 8)
                            
                            Capsule()
                                .fill(Color.white)
                                .frame(width: geometry.size.width * min(max(details.attendance.completionRate / 100, 0), 1), height: 8)
                        }
                    }
                    .frame(height: 8)
                    
                    Text("\(details.attendance.completionRate.toInt())% مكتمل")
                        .font(.custom("Tajawal-Regular", size: 11))
                        .foregroundColor(.white.opacity(0.8))
                        .frame(maxWidth: .infinity, alignment: .trailing)
                }
            }
            .padding(20)
            .background(
                LinearGradient(
                    colors: [BrandColors.teal, BrandColors.teal.opacity(0.85)],
                    startPoint: .topTrailing,
                    endPoint: .bottomLeading
                )
            )
            .cornerRadius(16)
            .shadow(color: BrandColors.teal.opacity(0.3), radius: 10, x: 0, y: 5)
        }
    }

    private func warningSummaryRow(_ warning: WarningDetails) -> some View {
        HStack(spacing: 12) {
            Image(systemName: "exclamationmark.triangle.fill")
                .foregroundColor(BrandColors.warningHigh)
                .frame(width: 40, height: 40)
                .background(BrandColors.warningHigh.opacity(0.1))
                .clipShape(Circle())
            VStack(alignment: .leading, spacing: 2) {
                Text(warning.reason)
                    .font(.custom("Tajawal-Medium", size: 13))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                Text(warning.severity)
                    .font(.custom("Tajawal-Regular", size: 11))
                    .foregroundColor(.gray)
            }
            Spacer()
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }
    
    private func metricWidgetCard(title: String, value: String, sub: String, icon: String, color: Color, onClick: @escaping () -> Void) -> some View {
        Button(action: onClick) {
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Text(title)
                        .font(.custom("Tajawal-Bold", size: 13))
                        .foregroundColor(.gray)
                    Spacer()
                    Image(systemName: icon)
                        .foregroundColor(color)
                }
                Text(value)
                    .font(.custom("Tajawal-Bold", size: 28))
                    .fontWeight(.black)
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                Text(sub)
                    .font(.custom("Tajawal-Bold", size: 11))
                    .foregroundColor(color)
            }
            .padding()
            .background(isDark ? BrandColors.darkCard : .white)
            .cornerRadius(16)
        }
    }
    
    // Tab 2: Schedule View (Screen 8)
    private func scheduleTab(details: StudentDetailsResponse) -> some View {
        let lessons = filteredWatchLessons(details)
        let hasDetailedLessons = !lessons.isEmpty
        let completedCount = hasDetailedLessons ? lessons.filter { $0.isCompleted }.count : details.attendance.watchedLessons
        let totalCount = hasDetailedLessons ? lessons.count : details.attendance.totalLessons
        let completionRate = hasDetailedLessons && totalCount > 0
            ? Double(completedCount) * 100 / Double(totalCount)
            : details.attendance.completionRate

        return VStack(spacing: 16) {
            academicFilterBar(details: details)

            watchSummaryCard(completed: completedCount, total: totalCount, rate: completionRate)
            if lessons.isEmpty {
                emptyDataCard(totalCount > 0
                    ? "تفاصيل الحصص غير متاحة حالياً. الملخص العام: \(completedCount) من \(totalCount) حصة."
                    : "لا توجد مشاهدات مسجلة حتى الآن.")
            } else {
                ForEach(lessons) { lesson in
                    scheduleItem(
                        time: lesson.isCompleted ? "مكتمل" : "قيد المشاهدة",
                        subject: lesson.lessonTitle,
                        teacher: "\(lesson.teacherName) • \(lesson.packageName) • \(lesson.termTitle)"
                    )
                }
            }
        }
    }

    private func watchSummaryCard(completed: Int, total: Int, rate: Double) -> some View {
        HStack(spacing: 20) {
            ZStack {
                Circle()
                    .stroke(BrandColors.softGray.opacity(0.5), lineWidth: 8)
                Circle()
                    .trim(from: 0, to: CGFloat(min(max(rate / 100, 0), 1)))
                    .stroke(BrandColors.teal, style: StrokeStyle(lineWidth: 8, lineCap: .round))
                    .rotationEffect(.degrees(-90))
                Text("\(rate.toInt())%")
                    .font(.custom("Tajawal-Bold", size: 16))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
            }
            .frame(width: 80, height: 80)

            VStack(alignment: .leading, spacing: 5) {
                Text("ملخص مشاهدة الحصص المختارة")
                    .font(.custom("Tajawal-Bold", size: 14))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                countStatusRow(label: "حصص مكتملة", count: completed, color: BrandColors.passGreen)
                countStatusRow(label: "تحتاج مشاهدة", count: max(total - completed, 0), color: BrandColors.warningHigh)
                countStatusRow(label: "إجمالي الحصص", count: total, color: BrandColors.teal)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(16)
    }

    /// Android opens these screens with the first available teacher/course/term
    /// selected. The computed fallbacks preserve that behaviour while allowing
    /// the user to change any picker without mutating view-model data.
    private func effectiveTeacherId(for details: StudentDetailsResponse) -> String? {
        guard !details.teachers.isEmpty else { return nil }
        if let selectedTeacherId,
           details.teachers.contains(where: { $0.teacherId == selectedTeacherId }) {
            return selectedTeacherId
        }
        return details.teachers.first?.teacherId
    }

    private func availableCourses(for details: StudentDetailsResponse) -> [CourseSummary] {
        guard let teacherId = effectiveTeacherId(for: details) else { return [] }
        return details.courses.filter { $0.teacherId == teacherId }
    }

    private func effectivePackageId(for details: StudentDetailsResponse) -> String? {
        let courses = availableCourses(for: details)
        guard !courses.isEmpty else { return nil }
        if let selectedPackageId,
           courses.contains(where: { $0.packageId == selectedPackageId }) {
            return selectedPackageId
        }
        return courses.first?.packageId
    }

    private func effectiveTermId(for details: StudentDetailsResponse) -> String? {
        guard let packageId = effectivePackageId(for: details),
              let course = availableCourses(for: details).first(where: { $0.packageId == packageId }),
              !course.terms.isEmpty else { return nil }
        if let selectedTermId,
           course.terms.contains(where: { $0.termId == selectedTermId }) {
            return selectedTermId
        }
        return course.terms.first?.termId
    }

    private func filteredWatchLessons(_ details: StudentDetailsResponse) -> [WatchLessonDetails] {
        let teacherId = effectiveTeacherId(for: details)
        let packageId = effectivePackageId(for: details)
        let termId = effectiveTermId(for: details)
        return details.watchLessons.filter { lesson in
            (teacherId == nil || lesson.teacherId == teacherId) &&
            (packageId == nil || lesson.packageId == packageId) &&
            (termId == nil || lesson.termId == termId)
        }
    }

    private func filteredExams(_ details: StudentDetailsResponse) -> [ExamDetails] {
        let teacherId = effectiveTeacherId(for: details)
        let packageId = effectivePackageId(for: details)
        let termId = effectiveTermId(for: details)
        return details.exams.filter { exam in
            (teacherId == nil || exam.teacherId == teacherId) &&
            (packageId == nil || exam.packageId == packageId) &&
            (termId == nil || exam.termId == termId)
        }
    }

    private func filteredHomeworks(_ details: StudentDetailsResponse) -> [HomeworkDetails] {
        let teacherId = effectiveTeacherId(for: details)
        let packageId = effectivePackageId(for: details)
        let termId = effectiveTermId(for: details)
        return details.homeworks.filter { homework in
            (teacherId == nil || homework.teacherId == teacherId) &&
            (packageId == nil || homework.packageId == packageId) &&
            (termId == nil || homework.termId == termId)
        }
    }

    private struct FilterOption: Identifiable {
        let id: String
        let title: String
    }

    private func academicFilterBar(details: StudentDetailsResponse) -> some View {
        let courses = availableCourses(for: details)
        let selectedCourseId = effectivePackageId(for: details)
        let selectedCourse = courses.first { $0.packageId == selectedCourseId }
        let terms = selectedCourse?.terms ?? []

        return ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                if !details.teachers.isEmpty {
                    filterMenu(
                        title: "المدرس",
                        selectedTitle: details.teachers.first(where: { $0.teacherId == effectiveTeacherId(for: details) })?.teacherName ?? "الكل",
                        options: details.teachers.map { FilterOption(id: $0.teacherId, title: $0.teacherName) },
                        onSelect: { selectedTeacherId = $0 }
                    )
                }

                if !courses.isEmpty {
                    filterMenu(
                        title: "الكورس",
                        selectedTitle: selectedCourse?.packageName ?? "الكل",
                        options: courses.map { FilterOption(id: $0.packageId, title: $0.packageName) },
                        onSelect: { selectedPackageId = $0 }
                    )
                }

                if !terms.isEmpty {
                    filterMenu(
                        title: "الترم",
                        selectedTitle: terms.first(where: { $0.termId == effectiveTermId(for: details) })?.termTitle ?? "الكل",
                        options: terms.map { FilterOption(id: $0.termId, title: $0.termTitle) },
                        onSelect: { selectedTermId = $0 }
                    )
                }
            }
            .padding(.vertical, 2)
        }
        .environment(\.layoutDirection, .rightToLeft)
    }

    private func teacherFilterBar(details: StudentDetailsResponse) -> some View {
        Group {
            if !details.teachers.isEmpty {
                filterMenu(
                    title: "المدرس",
                    selectedTitle: details.teachers.first(where: { $0.teacherId == effectiveTeacherId(for: details) })?.teacherName ?? "الكل",
                    options: details.teachers.map { FilterOption(id: $0.teacherId, title: $0.teacherName) },
                    onSelect: { selectedTeacherId = $0 }
                )
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func filterMenu(
        title: String,
        selectedTitle: String,
        options: [FilterOption],
        onSelect: @escaping (String) -> Void
    ) -> some View {
        Menu {
            ForEach(options) { option in
                Button {
                    onSelect(option.id)
                } label: {
                    Text(option.title)
                }
            }
        } label: {
            HStack(spacing: 5) {
                Text("\(title): \(selectedTitle)")
                    .lineLimit(1)
                Image(systemName: "chevron.down")
                    .font(.system(size: 9, weight: .bold))
            }
            .font(.custom("Tajawal-Medium", size: 11))
            .foregroundColor(isDark ? .white : BrandColors.deepNavy)
            .padding(.horizontal, 10)
            .padding(.vertical, 8)
            .background(isDark ? BrandColors.darkCard : Color.white)
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .stroke(BrandColors.softGray, lineWidth: 1)
            )
            .clipShape(RoundedRectangle(cornerRadius: 10))
        }
    }

    private func emptyDataCard(_ message: String) -> some View {
        Text(message)
            .font(.custom("Tajawal-Regular", size: 13))
            .foregroundColor(.gray)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding()
            .background(isDark ? BrandColors.darkCard : .white)
            .cornerRadius(12)
    }
    
    private func scheduleItem(time: String, subject: String, teacher: String) -> some View {
        HStack {
            Text(time)
                .font(.custom("Tajawal-Bold", size: 13))
                .foregroundColor(BrandColors.teal)
                .frame(width: 70, alignment: .leading)
            Spacer()
            VStack(alignment: .leading) {
                Text(subject)
                    .font(.custom("Tajawal-Bold", size: 14))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                Text(teacher)
                    .font(.custom("Tajawal-Regular", size: 12))
                    .foregroundColor(.gray)
            }
            Spacer()
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }
    
    // Tab 3: Homework View (Screen 7)
    private func homeworkTab(details: StudentDetailsResponse) -> some View {
        let filtered = filteredHomeworks(details).filter { homework in
            switch homeworkFilter {
            case .all: return true
            case .pending: return !homework.isSubmitted
            case .submitted: return homework.isSubmitted
            }
        }

        return VStack(spacing: 12) {
            academicFilterBar(details: details)

            Picker("حالة الواجب", selection: $homeworkFilter) {
                ForEach(HomeworkFilter.allCases, id: \.self) { filter in
                    Text(filter.rawValue).tag(filter)
                }
            }
            .pickerStyle(.segmented)
            .environment(\.layoutDirection, .rightToLeft)

            if filtered.isEmpty {
                emptyDataCard("لا توجد واجبات في هذا التصنيف.")
            } else {
                ForEach(filtered) { hw in
                    homeworkCard(hw)
                }
            }
        }
    }

    private func homeworkCard(_ homework: HomeworkDetails) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(homework.title)
                        .font(.custom("Tajawal-Bold", size: 14))
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                    Text([homework.teacherName, homework.packageName, homework.termTitle]
                        .compactMap { $0 }
                        .joined(separator: " • "))
                        .font(.custom("Tajawal-Regular", size: 11))
                        .foregroundColor(.gray)
                    Text(homework.isSubmitted ? "التقييم: \(homework.grade ?? "غير مرصود")" : "متبقي للتسليم")
                        .font(.custom("Tajawal-Regular", size: 11))
                        .foregroundColor(homework.isSubmitted ? BrandColors.teal : BrandColors.warningHigh)
                }
                Spacer()
                Text(homework.isSubmitted ? "تم التسليم" : "متبقي")
                    .font(.custom("Tajawal-Bold", size: 12))
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(homework.isSubmitted ? BrandColors.teal.opacity(0.1) : BrandColors.warningHigh.opacity(0.1))
                    .foregroundColor(homework.isSubmitted ? BrandColors.teal : BrandColors.warningHigh)
                    .cornerRadius(8)
            }
            if let mistakes = homework.mistakes, !mistakes.isEmpty {
                homeworkMistakesView(mistakes)
            }
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }

    private func homeworkMistakesView(_ mistakes: [HomeworkAnswerReview]) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("مراجعة الأخطاء (\(mistakes.count))")
                .font(.custom("Tajawal-Bold", size: 12))
                .foregroundColor(BrandColors.warningHigh)
            ForEach(Array(mistakes.enumerated()), id: \.offset) { _, mistake in
                VStack(alignment: .leading, spacing: 3) {
                    Text(mistake.questionText)
                        .font(.custom("Tajawal-Medium", size: 12))
                    Text("إجابتك: \(mistake.studentAnswer)")
                        .font(.custom("Tajawal-Regular", size: 11))
                        .foregroundColor(.red)
                    if let correctAnswer = mistake.correctAnswer {
                        Text("الإجابة الصحيحة: \(correctAnswer)")
                            .font(.custom("Tajawal-Regular", size: 11))
                            .foregroundColor(BrandColors.passGreen)
                    }
                    if let correction = mistake.writtenCorrection, !correction.isEmpty {
                        Text("تصحيح المدرس: \(correction)")
                            .font(.custom("Tajawal-Regular", size: 11))
                            .foregroundColor(.gray)
                    }
                }
                .padding(8)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(BrandColors.softGray.opacity(isDark ? 0.12 : 0.45))
                .cornerRadius(8)
            }
        }
    }
    
    // Tab 4: Grades View (Screen 6)
    private func gradesTab(details: StudentDetailsResponse) -> some View {
        let exams = filteredExams(details)
        return VStack(spacing: 12) {
            academicFilterBar(details: details)

            gradeSummaryCard(exams: exams)

            if exams.isEmpty {
                emptyDataCard("لا توجد امتحانات في هذا الاختيار.")
            } else {
                ForEach(exams) { exam in
                    examCard(exam)
                }
            }
        }
    }

    private func gradeSummaryCard(exams: [ExamDetails]) -> some View {
        VStack(alignment: .leading) {
            Text("متوسط درجات الطالب")
                .font(.custom("Tajawal-Bold", size: 13))
                .foregroundColor(.gray)
            Text(exams.isEmpty ? "لا توجد محاولات مسجلة" : "\(Int(exams.map(\.percentage).reduce(0, +) / Double(exams.count)))% متوسط الدرجات")
                .font(.custom("Tajawal-Bold", size: 20))
                .foregroundColor(BrandColors.teal)
            if !exams.isEmpty { gradeChart(exams: exams) }
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(16)
    }

    private func gradeChart(exams: [ExamDetails]) -> some View {
        GeometryReader { geometry in
            Path { path in
                let width = geometry.size.width
                let height = geometry.size.height
                let maxIndex = max(exams.count - 1, 1)
                for (index, exam) in exams.enumerated() {
                    let x = width * CGFloat(index) / CGFloat(maxIndex)
                    let y = height * (1 - CGFloat(min(max(exam.percentage, 0), 100)) / 100)
                    if index == 0 { path.move(to: CGPoint(x: x, y: y)) }
                    else { path.addLine(to: CGPoint(x: x, y: y)) }
                }
            }
            .stroke(BrandColors.teal, lineWidth: 3)
        }
        .frame(height: 80)
    }

    private func examCard(_ exam: ExamDetails) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(exam.examTitle)
                        .font(.custom("Tajawal-Bold", size: 13))
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                    Text([exam.teacherName, exam.packageName, exam.termTitle]
                        .compactMap { $0 }
                        .joined(separator: " • "))
                        .font(.custom("Tajawal-Regular", size: 11))
                        .foregroundColor(.gray)
                    Text("الدرجة: \(exam.score) / \(exam.totalScore) (\(exam.percentage.toInt())%)")
                        .font(.custom("Tajawal-Regular", size: 12))
                        .foregroundColor(.gray)
                }
                Spacer()
                Text(examStatusTitle(exam.status))
                    .font(.custom("Tajawal-Bold", size: 12))
                    .foregroundColor(examStatusColor(exam.status))
            }
            if let mistakes = exam.mistakes, !mistakes.isEmpty {
                examMistakesView(mistakes)
            }
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }

    private func examStatusTitle(_ status: String) -> String {
        switch status.lowercased() {
        case "passed", "pass", "ناجح": return "ناجح"
        case "failed", "fail", "راسب": return "راسب"
        default: return "لم يبدأ"
        }
    }

    private func examStatusColor(_ status: String) -> Color {
        switch status.lowercased() {
        case "passed", "pass", "ناجح": return BrandColors.passGreen
        case "failed", "fail", "راسب": return BrandColors.warningHigh
        default: return .gray
        }
    }

    private func examMistakesView(_ mistakes: [QuestionReview]) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("مراجعة الأخطاء (\(mistakes.count))")
                .font(.custom("Tajawal-Bold", size: 12))
                .foregroundColor(BrandColors.warningHigh)
            ForEach(Array(mistakes.enumerated()), id: \.offset) { item in
                let mistake = item.element
                VStack(alignment: .leading, spacing: 3) {
                    Text(mistake.questionText)
                        .font(.custom("Tajawal-Medium", size: 12))
                    if let answer = mistake.studentAnswer {
                        Text("إجابتك: \(answer)")
                            .font(.custom("Tajawal-Regular", size: 11))
                            .foregroundColor(.red)
                    }
                    if let correctAnswer = mistake.correctAnswer {
                        Text("الإجابة الصحيحة: \(correctAnswer)")
                            .font(.custom("Tajawal-Regular", size: 11))
                            .foregroundColor(BrandColors.passGreen)
                    }
                    if let correction = mistake.writtenCorrection, !correction.isEmpty {
                        Text("تصحيح المدرس: \(correction)")
                            .font(.custom("Tajawal-Regular", size: 11))
                            .foregroundColor(.gray)
                    }
                }
                .padding(8)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(BrandColors.softGray.opacity(isDark ? 0.12 : 0.45))
                .cornerRadius(8)
            }
        }
    }
    
    // Tab 5: More Menu View
    private func moreTab() -> some View {
        VStack(spacing: 12) {
            moreMenuRow(title: "ربط طالب جديد", iconName: "plus.circle.fill") { onAddStudent() }
            moreMenuRow(title: "الملف الشخصي للطالب", iconName: "person.fill") { activeSubScreen = "profile" }
            moreMenuRow(title: "الكورسات والترمات", iconName: "list.bullet") { activeSubScreen = "courses" }
            moreMenuRow(title: "سجل المشاهدات", iconName: "play.fill") { activeSubScreen = "attendance" }
            moreMenuRow(title: "ملاحظات المدرسين", iconName: "info.circle.fill") { activeSubScreen = "notes" }
            moreMenuRow(title: "الرصيد وتفاصيل الرصيد", iconName: "cart.fill") { activeSubScreen = "fees" }
            moreMenuRow(title: "إعدادات التطبيق", iconName: "gearshape.fill") { activeSubScreen = "settings" }
        }
    }
    
    private func moreMenuRow(title: String, iconName: String, onClick: @escaping () -> Void) -> some View {
        Button(action: onClick) {
            HStack {
                Image(systemName: iconName)
                    .foregroundColor(BrandColors.teal)
                    .font(.system(size: 18))
                Spacer()
                    .frame(width: 16)
                Text(title)
                    .font(.custom("Tajawal-Bold", size: 14))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                Spacer()
                Image(systemName: "chevron.left")
                    .foregroundColor(.gray.opacity(0.5))
                    .font(.system(size: 14))
            }
            .padding()
            .background(isDark ? BrandColors.darkCard : .white)
            .cornerRadius(12)
        }
    }
    
    // --- Sub-Screens Detail Views ---
    
    // Sub-view Top Bar Helper
    private func subViewTopBar(title: String) -> some View {
        HStack {
            Button(action: { activeSubScreen = nil }) {
                Image(systemName: "chevron.right")
                    .font(.system(size: 18, weight: .bold))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
            }
            Spacer()
            Text(title)
                .font(.custom("Tajawal-Bold", size: 18))
                .fontWeight(.bold)
                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
            Spacer()
            Spacer()
                .frame(width: 24)
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
    }
    
    // Screen 4: Student Profile View
    private func profileView(name: String, grade: String, school: String) -> some View {
        VStack(spacing: 0) {
            subViewTopBar(title: "الملف الشخصي")
            
            VStack(spacing: 24) {
                // Profile card
                VStack(spacing: 16) {
                    Circle()
                        .fill(BrandColors.deepNavy)
                        .frame(width: 80, height: 80)
                        .overlay(
                            Text(String(name.prefix(1)))
                                .font(.custom("Tajawal-Bold", size: 32))
                                .foregroundColor(.white)
                        )
                    Text(name)
                        .font(.custom("Tajawal-Bold", size: 20))
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                    Text("رقم المتابعة: \(viewModel.selectedProfile?.studentId.prefix(8) ?? "-")")
                        .font(.custom("Tajawal-Bold", size: 13))
                        .foregroundColor(BrandColors.teal)
                }
                .frame(maxWidth: .infinity)
                .padding()
                .background(isDark ? BrandColors.darkCard : .white)
                .cornerRadius(16)
                
                // Info rows
                VStack(spacing: 12) {
                    infoRow(label: "الصف الدراسي", value: grade, iconName: "house.fill")
                    infoRow(label: "المدرسة", value: school, iconName: "building.2.fill")
                    infoRow(label: "عدد المدرسين", value: "\(viewModel.studentDetails?.teachers.count ?? 0) مدرس", iconName: "person.fill")
                }
                
                Spacer()
            }
            .padding()
        }
    }
    
    private func infoRow(label: String, value: String, iconName: String) -> some View {
        HStack {
            Image(systemName: iconName)
                .foregroundColor(BrandColors.teal)
            Spacer()
                .frame(width: 16)
            Text(label)
                .font(.custom("Tajawal-Bold", size: 13))
                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
            Spacer()
            Text(value)
                .font(.custom("Tajawal-Bold", size: 13))
                .foregroundColor(.gray)
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }
    
    // Screen 5: Attendance Calendar & Gauge View
    private func attendanceView(watched: Int, total: Int, rate: Double) -> some View {
        VStack(spacing: 0) {
            subViewTopBar(title: "سجل المشاهدات")
            
            ScrollView {
                VStack(spacing: 16) {
                    // Gauge card
                    VStack(alignment: .leading) {
                        Text("نسبة الالتزام الإجمالية")
                            .font(.custom("Tajawal-Bold", size: 14))
                            .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                            .padding(.bottom, 8)
                        
                        HStack(spacing: 24) {
                            ZStack {
                                Circle()
                                    .stroke(Color.gray.opacity(0.1), lineWidth: 8)
                                Circle()
                                    .trim(from: 0, to: CGFloat(min(max(rate / 100, 0), 1)))
                                    .stroke(BrandColors.teal, style: StrokeStyle(lineWidth: 8, lineCap: .round))
                                    .rotationEffect(.degrees(-90))
                                Text("\(rate.toInt())%")
                                    .font(.custom("Tajawal-Bold", size: 16))
                            }
                            .frame(width: 80, height: 80)
                            
                            VStack(alignment: .leading, spacing: 4) {
                                countStatusRow(label: "حاضر ومكتمل", count: watched, color: BrandColors.passGreen)
                                countStatusRow(label: "حصص تحتاج مشاهدة", count: max(total - watched, 0), color: BrandColors.warningHigh)
                            }
                        }
                    }
                    .padding()
                    .background(isDark ? BrandColors.darkCard : .white)
                    .cornerRadius(16)
                }
                .padding()
            }
        }
    }
    
    private func countStatusRow(label: String, count: Int, color: Color) -> some View {
        HStack(spacing: 8) {
            Circle()
                .fill(color)
                .frame(width: 8, height: 8)
            Text(label)
                .font(.custom("Tajawal-Bold", size: 12))
            Spacer()
            Text("\(count)")
                .font(.custom("Tajawal-Bold", size: 13))
                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
        }
    }

    private func coursesView(_ courses: [CourseSummary]) -> some View {
        let details = viewModel.studentDetails
        let filteredCourses: [CourseSummary]
        if let details, let teacherId = effectiveTeacherId(for: details) {
            filteredCourses = courses.filter { $0.teacherId == teacherId }
        } else {
            filteredCourses = []
        }

        return VStack(spacing: 12) {
            if let details {
                teacherFilterBar(details: details)
            }

            if filteredCourses.isEmpty {
                emptyDataCard("لا توجد كورسات مرتبطة بهذا الطالب حتى الآن.")
            } else {
                ForEach(filteredCourses) { course in
                    VStack(alignment: .leading, spacing: 6) {
                        HStack {
                            Image(systemName: "books.vertical.fill")
                                .foregroundColor(BrandColors.teal)
                            Text(course.packageName)
                                .font(.custom("Tajawal-Bold", size: 15))
                                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                            Spacer()
                        }
                        Text("المدرس: \(course.teacherName)")
                            .font(.custom("Tajawal-Regular", size: 12))
                            .foregroundColor(.gray)
                        ForEach(course.terms) { term in
                            HStack {
                                Text(term.termTitle)
                                    .font(.custom("Tajawal-Medium", size: 12))
                                Spacer()
                                Text("\(term.lessonCount) درس")
                                Text("\(term.examCount) اختبار")
                            }
                            .font(.custom("Tajawal-Regular", size: 11))
                            .foregroundColor(BrandColors.teal)
                            .padding(.vertical, 4)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding()
                    .background(isDark ? BrandColors.darkCard : .white)
                    .cornerRadius(12)
                }
            }
        }
    }
    
    // Screen 9: Teacher Notes View
    private func teacherNotesView() -> some View {
        VStack(spacing: 0) {
            subViewTopBar(title: "ملاحظات المدرسين")
            
            ScrollView {
                VStack(spacing: 12) {
                    if let teachers = viewModel.studentDetails?.teachers, !teachers.isEmpty {
                        ForEach(teachers) { teacher in
                            noteCard(teacher: teacher.teacherName, note: teacher.specialization ?? "لا توجد ملاحظات مسجلة من هذا المدرس.")
                        }
                    } else {
                        emptyDataCard("لا توجد ملاحظات أو بيانات مدرسين حتى الآن.")
                    }
                }
                .padding()
            }
        }
    }
    
    private func noteCard(teacher: String, note: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Image(systemName: "info.circle.fill")
                    .foregroundColor(BrandColors.teal)
                Text(teacher)
                    .font(.custom("Tajawal-Bold", size: 14))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
            }
            Text(note)
                .font(.custom("Tajawal-Regular", size: 13))
                .foregroundColor(.gray)
                .lineSpacing(4)
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }
    
    // Screen 10: Fees / Payment View
    private func feesView() -> some View {
        let balance = viewModel.studentDetails?.balance
        return VStack(spacing: 0) {
            subViewTopBar(title: "الرصيد وتفاصيل الرصيد")
            
            VStack(spacing: 16) {
                VStack(spacing: 8) {
                    Text("الرصيد الحالي")
                        .font(.custom("Tajawal-Regular", size: 13))
                        .foregroundColor(.gray)
                    Text("\(String(format: "%.2f", balance?.currentBalance ?? 0)) ج.م")
                        .font(.custom("Tajawal-Bold", size: 28))
                        .fontWeight(.black)
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                }
                .frame(maxWidth: .infinity)
                .padding()
                .background(isDark ? BrandColors.darkCard : .white)
                .cornerRadius(16)
                
                if let transactions = balance?.transactions, !transactions.isEmpty {
                    ForEach(transactions) { transaction in
                        feeDetailRow(label: transaction.description, value: "\(String(format: "%.2f", transaction.amount)) ج.م", isCompleted: transaction.amount >= 0)
                    }
                } else {
                    emptyDataCard("لا توجد حركات رصيد مسجلة حتى الآن.")
                }
            }
            .padding()
        }
    }
    
    private func feeDetailRow(label: String, value: String, isCompleted: Bool) -> some View {
        HStack {
            Image(systemName: isCompleted ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
                .foregroundColor(isCompleted ? BrandColors.passGreen : BrandColors.warningHigh)
            Text(label)
                .font(.custom("Tajawal-Bold", size: 13))
                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
            Spacer()
            Text(value)
                .font(.custom("Tajawal-Bold", size: 14))
                .foregroundColor(isCompleted ? BrandColors.passGreen : BrandColors.warningHigh)
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }
    
    // Screen 11: Notifications / Alerts View
    private func notificationsView() -> some View {
        VStack(spacing: 0) {
            subViewTopBar(title: "التنبيهات والإشعارات")
            
            ScrollView {
                VStack(spacing: 12) {
                    if viewModel.notifications.isEmpty {
                        if viewModel.studentDetails?.warnings.isEmpty ?? true {
                            emptyDataCard("لا توجد تنبيهات مسجلة حتى الآن.")
                        }
                    }
                    if !viewModel.notifications.isEmpty {
                        ForEach(viewModel.notifications) { notification in
                            Button {
                                Task { await viewModel.markNotificationAsRead(notification) }
                            } label: {
                                notificationRow(title: notification.title, desc: notification.body, date: notification.createdAt, color: notification.isRead ? .gray : BrandColors.teal)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    ForEach((viewModel.studentDetails?.warnings ?? []).sorted { $0.createdAt > $1.createdAt }) { warning in
                        notificationRow(title: "تنبيه \(warning.severity)", desc: warning.reason, date: warning.createdAt, color: BrandColors.warningHigh)
                    }
                }
                .padding()
            }
        }
    }
    
    private func notificationRow(title: String, desc: String, date: String, color: Color) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Circle()
                .fill(color.opacity(0.1))
                .frame(width: 36, height: 36)
                .overlay(
                    Image(systemName: "bell.fill")
                        .foregroundColor(color)
                        .font(.system(size: 14))
                )
            
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.custom("Tajawal-Bold", size: 13))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                Text(desc)
                    .font(.custom("Tajawal-Regular", size: 12))
                    .foregroundColor(.gray)
                Text(displayDate(date))
                    .font(.custom("Tajawal-Regular", size: 10))
                    .foregroundColor(.gray.opacity(0.6))
            }
            Spacer()
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }

    private func displayDate(_ rawDate: String) -> String {
        let isoFormatter = ISO8601DateFormatter()
        isoFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        guard let parsedDate = isoFormatter.date(from: rawDate)
                ?? ISO8601DateFormatter().date(from: rawDate) else { return rawDate }
        let displayFormatter = DateFormatter()
        displayFormatter.locale = Locale(identifier: "ar_EG")
        displayFormatter.dateFormat = "dd/MM/yyyy HH:mm"
        return displayFormatter.string(from: parsedDate)
    }
    
    // Screen 12: App Settings View
    private func settingsView() -> some View {
        VStack(spacing: 0) {
            subViewTopBar(title: "الإعدادات")
            
            VStack(spacing: 12) {
                HStack {
                    Image(systemName: "bell.fill")
                        .foregroundColor(BrandColors.teal)
                    Text(notificationsEnabled ? "الإشعارات مفعلة" : "الإشعارات غير مفعلة")
                        .font(.custom("Tajawal-Bold", size: 13))
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                    Spacer()
                    Toggle("", isOn: $notificationsEnabled)
                        .labelsHidden()
                        .tint(BrandColors.teal)
                        .onChange(of: notificationsEnabled) { _, enabled in
                            guard enabled else { return }
                            Task {
                                notificationsEnabled = await ParentNotificationService.requestAuthorization()
                            }
                        }
                }
                .padding()
                .background(isDark ? BrandColors.darkCard : .white)
                .cornerRadius(12)

                Button {
                    Task { _ = await ParentNotificationService.showTestNotification() }
                } label: {
                    settingsRow(title: "اختبار إشعار داخل التطبيق", iconName: "checkmark.message.fill")
                }
                .buttonStyle(.plain)
                settingsRow(title: "تغيير رقم موبايل ولي الأمر", iconName: "phone.fill")
                settingsRow(title: "الدعم الفني والشكاوى", iconName: "wrench.and.screwdriver.fill")
                settingsRow(title: "الأسئلة الشائعة FAQ", iconName: "questionmark.circle.fill")
                settingsRow(title: "عن منصة مسار", iconName: "info.circle.fill")
                
                Spacer()
                    .frame(height: 24)
                
                // Logout
                Button(action: {
                    guard let profile = viewModel.selectedProfile else { return }
                    Task {
                        await viewModel.removeProfile(profile)
                        activeSubScreen = nil
                        if viewModel.selectedProfile == nil {
                            onAddStudent()
                        }
                    }
                }) {
                    HStack {
                        Image(systemName: "rectangle.portrait.and.arrow.right")
                        Text("تسجيل الخروج من الحساب")
                    }
                    .font(.custom("Tajawal-Bold", size: 14))
                    .foregroundColor(.red)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding()
                    .background(isDark ? BrandColors.darkCard : .white)
                    .cornerRadius(12)
                }
            }
            .padding()
        }
        .task {
            notificationsEnabled = await ParentNotificationService.authorizationStatus()
        }
    }
    
    private func settingsRow(title: String, iconName: String) -> some View {
        HStack {
            Image(systemName: iconName)
                .foregroundColor(BrandColors.teal)
            Spacer()
                .frame(width: 16)
            Text(title)
                .font(.custom("Tajawal-Bold", size: 13))
                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
            Spacer()
            Image(systemName: "chevron.left")
                .foregroundColor(.gray.opacity(0.5))
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }
}

// Float conversion helper
extension Double {
    func toInt() -> Int {
        return Int(self)
    }
}
