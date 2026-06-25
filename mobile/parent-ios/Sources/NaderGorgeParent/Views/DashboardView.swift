import SwiftUI

@MainActor
public struct DashboardView: View {
    @StateObject private var viewModel: DashboardViewModel
    @State private var selectedTab: Tab = .home
    @State private var activeSubScreen: String? = nil // "profile", "attendance", "notes", "fees", "notifications", "settings"
    @State private var showStudentSelector = false
    @Environment(\.colorScheme) var colorScheme
    
    public var onAddStudent: () -> Void
    
    public enum Tab: String, CaseIterable {
        case home = "الرئيسية"
        case schedule = "الجدول"
        case homework = "الواجبات"
        case grades = "الدرجات"
        case more = "المزيد"
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
                                scheduleTab()
                            case .homework:
                                homeworkTab(homeworks: details.homeworks)
                            case .grades:
                                gradesTab(exams: details.exams)
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
            tabItem(tab: .schedule, iconName: "calendar", activeIconName: "calendar")
            tabItem(tab: .homework, iconName: "pencil.and.outline", activeIconName: "pencil.and.outline")
            tabItem(tab: .grades, iconName: "star", activeIconName: "star.fill")
            tabItem(tab: .more, iconName: "ellipsis.circle", activeIconName: "ellipsis.circle.fill")
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
        let name = details?.studentName ?? viewModel.selectedProfile?.name ?? "أحمد محمد"
        let grade = details?.grade ?? "الصف الدراسي"
        let school = details?.school ?? "مدرسة مسار"
        
        switch subScreen {
        case "profile":
            profileView(name: name, grade: grade, school: school)
        case "attendance":
            attendanceView(
                watched: details?.attendance.watchedLessons ?? 18,
                total: details?.attendance.totalLessons ?? 20,
                rate: details?.attendance.completionRate ?? 90.0
            )
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
        VStack(spacing: 16) {
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
                    metricWidgetCard(title: "الحضور", value: "\(details.attendance.completionRate.toInt())%", sub: "نسبة الالتزام", icon: "checkmark.circle.fill", color: BrandColors.teal) {
                        activeSubScreen = "attendance"
                    }
                    metricWidgetCard(title: "آخر درجة", value: "88", sub: "ممتاز", icon: "star.fill", color: BrandColors.warmGold) {
                        selectedTab = .grades
                    }
                }
                HStack(spacing: 12) {
                    metricWidgetCard(title: "الواجبات", value: "\(details.homeworks.filter { !$0.isSubmitted }.count)", sub: "واجبات متبقية", icon: "pencil.and.outline", color: BrandColors.deepNavy) {
                        selectedTab = .homework
                    }
                    metricWidgetCard(title: "الإنذارات", value: "\(details.warnings.count)", sub: "تنبيه إرشادي", icon: "exclamationmark.triangle.fill", color: BrandColors.warningHigh) {
                        activeSubScreen = "notifications"
                    }
                }
            }
            
            // Alert Banner (Titled "تنبيه مهم جداً")
            HStack(spacing: 12) {
                Image(systemName: "exclamationmark.triangle.fill")
                    .foregroundColor(BrandColors.warmGold)
                    .font(.system(size: 18))
                    .padding(8)
                    .background(BrandColors.warmGold.opacity(0.15))
                    .clipShape(Circle())
                
                VStack(alignment: .leading, spacing: 2) {
                    Text("تنبيه مهم جداً")
                        .font(.custom("Tajawal-Bold", size: 13))
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                    Text("موعد اختبار الرياضيات الشامل يوم الأحد القادم 12 مايو.")
                        .font(.custom("Tajawal-Regular", size: 12))
                        .foregroundColor(.gray)
                }
                Spacer()
            }
            .padding()
            .background(BrandColors.warmGold.opacity(isDark ? 0.08 : 0.12))
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(BrandColors.warmGold.opacity(0.2), lineWidth: 1)
            )
            
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
                
                // List View (Variant A)
                VStack(spacing: 12) {
                    HStack(spacing: 12) {
                        ZStack {
                            Circle()
                                .fill(BrandColors.teal.opacity(0.1))
                                .frame(width: 40, height: 40)
                            Image(systemName: "star.bubble.fill")
                                .font(.system(size: 16))
                                .foregroundColor(BrandColors.teal)
                        }
                        
                        VStack(alignment: .leading, spacing: 2) {
                            Text("تم رصد درجة اختبار اللغة العربية")
                                .font(.custom("Tajawal-Medium", size: 13))
                                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                            Text("منذ ساعتين")
                                .font(.custom("Tajawal-Regular", size: 11))
                                .foregroundColor(.gray)
                        }
                        Spacer()
                    }
                    .padding()
                    .background(isDark ? BrandColors.darkCard : .white)
                    .cornerRadius(12)
                    .shadow(color: Color.black.opacity(isDark ? 0.2 : 0.03), radius: 4, x: 0, y: 2)
                    
                    HStack(spacing: 12) {
                        ZStack {
                            Circle()
                                .fill(BrandColors.warningHigh.opacity(0.1))
                                .frame(width: 40, height: 40)
                            Image(systemName: "person.badge.minus.fill")
                                .font(.system(size: 16))
                                .foregroundColor(BrandColors.warningHigh)
                        }
                        
                        VStack(alignment: .leading, spacing: 2) {
                            Text("تسجيل غياب في الحصة الثالثة")
                                .font(.custom("Tajawal-Medium", size: 13))
                                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                            Text("أمس")
                                .font(.custom("Tajawal-Regular", size: 11))
                                .foregroundColor(.gray)
                        }
                        Spacer()
                    }
                    .padding()
                    .background(isDark ? BrandColors.darkCard : .white)
                    .cornerRadius(12)
                    .shadow(color: Color.black.opacity(isDark ? 0.2 : 0.03), radius: 4, x: 0, y: 2)
                }
            }
            
            // General Academic Progress Visual Card
            VStack(alignment: .leading, spacing: 12) {
                Text("التقدم الأكاديمي العام")
                    .font(.custom("Tajawal-Medium", size: 12))
                    .foregroundColor(.white.opacity(0.8))
                
                Text("أداء متميز هذا الفصل")
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
                                .frame(width: geometry.size.width * 0.75, height: 8)
                        }
                    }
                    .frame(height: 8)
                    
                    Text("75% مكتمل")
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
    private func scheduleTab() -> some View {
        VStack(spacing: 16) {
            scheduleItem(time: "08:00 ص", subject: "اللغة العربية", teacher: "أ. أحمد سعيد")
            scheduleItem(time: "09:00 ص", subject: "الرياضيات التطبيقية", teacher: "أ. محمد خالد")
            scheduleItem(time: "10:00 ص", subject: "العلوم الفيزيائية", teacher: "أ. سارة جمال")
            
            // Break item
            HStack {
                Spacer()
                Text("استراحة غداء ونشاط حر")
                    .font(.custom("Tajawal-Bold", size: 12))
                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                Spacer()
            }
            .padding(12)
            .background(isDark ? BrandColors.darkCard : BrandColors.softGray)
            .cornerRadius(12)
            
            scheduleItem(time: "11:30 ص", subject: "اللغة الإنجليزية", teacher: "أ. ندى محمود")
            scheduleItem(time: "12:30 م", subject: "الدراسات الاجتماعية", teacher: "أ. عمرو عبد الله")
        }
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
    private func homeworkTab(homeworks: [HomeworkDetails]) -> some View {
        VStack(spacing: 12) {
            ForEach(homeworks, id: \.title) { hw in
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(hw.title)
                            .font(.custom("Tajawal-Bold", size: 14))
                            .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                        if hw.isSubmitted {
                            Text("التقييم: \(hw.grade ?? "A")")
                                .font(.custom("Tajawal-Regular", size: 11))
                                .foregroundColor(BrandColors.teal)
                        } else {
                            Text("متأخر عن التسليم")
                                .font(.custom("Tajawal-Regular", size: 11))
                                .foregroundColor(.red)
                        }
                    }
                    Spacer()
                    Text(hw.isSubmitted ? "مسلم" : "متبقي")
                        .font(.custom("Tajawal-Bold", size: 12))
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(hw.isSubmitted ? BrandColors.teal.opacity(0.1) : Color.red.opacity(0.1))
                        .foregroundColor(hw.isSubmitted ? BrandColors.teal : .red)
                        .cornerRadius(8)
                }
                .padding()
                .background(isDark ? BrandColors.darkCard : .white)
                .cornerRadius(12)
            }
        }
    }
    
    // Tab 4: Grades View (Screen 6)
    private func gradesTab(exams: [ExamDetails]) -> some View {
        VStack(spacing: 12) {
            // Line Chart Visual representation
            VStack(alignment: .leading) {
                Text("متوسط درجات الطالب")
                    .font(.custom("Tajawal-Bold", size: 13))
                    .foregroundColor(.gray)
                Text("88% (ممتاز)")
                    .font(.custom("Tajawal-Bold", size: 20))
                    .foregroundColor(BrandColors.teal)
                
                // Chart path
                GeometryReader { geometry in
                    Path { path in
                        let w = geometry.size.width
                        let h = geometry.size.height
                        path.move(to: CGPoint(x: 0, y: h * 0.7))
                        path.addLine(to: CGPoint(x: w * 0.3, y: h * 0.7))
                        path.addLine(to: CGPoint(x: w * 0.6, y: h * 0.5))
                        path.addLine(to: CGPoint(x: w, y: h * 0.3))
                    }
                    .stroke(BrandColors.teal, lineWidth: 3)
                }
                .frame(height: 80)
            }
            .padding()
            .background(isDark ? BrandColors.darkCard : .white)
            .cornerRadius(16)
            
            // List of tests
            ForEach(exams, id: \.examTitle) { exam in
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(exam.examTitle)
                            .font(.custom("Tajawal-Bold", size: 13))
                            .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                        Text("الدرجة: \(exam.score) / \(exam.totalScore) (\(exam.percentage.toInt())%)")
                            .font(.custom("Tajawal-Regular", size: 12))
                            .foregroundColor(.gray)
                    }
                    Spacer()
                    Text(exam.status == "Passed" ? "ناجح" : "راسب")
                        .font(.custom("Tajawal-Bold", size: 12))
                        .foregroundColor(exam.status == "Passed" ? BrandColors.passGreen : .red)
                }
                .padding()
                .background(isDark ? BrandColors.darkCard : .white)
                .cornerRadius(12)
            }
        }
    }
    
    // Tab 5: More Menu View
    private func moreTab() -> some View {
        VStack(spacing: 12) {
            moreMenuRow(title: "الملف الشخصي للطالب", iconName: "person.fill") { activeSubScreen = "profile" }
            moreMenuRow(title: "سجل الغياب والحضور", iconName: "checkmark.circle.fill") { activeSubScreen = "attendance" }
            moreMenuRow(title: "ملاحظات المدرسين", iconName: "info.circle.fill") { activeSubScreen = "notes" }
            moreMenuRow(title: "المصاريف والمدفوعات", iconName: "cart.fill") { activeSubScreen = "fees" }
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
                    Text("رقم المتابعة: MSR-2026-00125")
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
                    infoRow(label: "المجموعة الدراسية", value: "مجموعة A", iconName: "star.fill")
                    infoRow(label: "تاريخ الميلاد", value: "15/05/2009", iconName: "calendar")
                    infoRow(label: "عدد المدرسين", value: "6 مدرسين", iconName: "person.fill")
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
            subViewTopBar(title: "الحضور والغياب")
            
            ScrollView {
                VStack(spacing: 16) {
                    // Calendar card
                    VStack {
                        HStack {
                            Image(systemName: "chevron.right")
                            Spacer()
                            Text("مايو 2026")
                                .font(.custom("Tajawal-Bold", size: 15))
                            Spacer()
                            Image(systemName: "chevron.left")
                        }
                        .padding(.bottom, 8)
                        
                        // Simple grid drawing
                        simpleCalendarGrid
                    }
                    .padding()
                    .background(isDark ? BrandColors.darkCard : .white)
                    .cornerRadius(16)
                    
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
                                    .stroke(BrandColors.teal, lineWidth: 8)
                                Text("\(rate.toInt())%")
                                    .font(.custom("Tajawal-Bold", size: 16))
                            }
                            .frame(width: 80, height: 80)
                            
                            VStack(alignment: .leading, spacing: 4) {
                                countStatusRow(label: "حاضر ومكتمل", count: watched, color: BrandColors.passGreen)
                                countStatusRow(label: "غائب ومتأخر", count: total - watched, color: BrandColors.warningHigh)
                                countStatusRow(label: "إجازة رسمية", count: 2, color: .gray)
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
    
    private var simpleCalendarGrid: some View {
        VStack(spacing: 8) {
            HStack {
                ForEach(["س", "ح", "ن", "ث", "ر", "خ", "ج"], id: \.self) { day in
                    Text(day)
                        .font(.custom("Tajawal-Bold", size: 11))
                        .frame(maxWidth: .infinity)
                }
            }
            Divider()
            ForEach(0..<4) { row in
                HStack {
                    ForEach(1...7, id: \.self) { col in
                        let dateNum = row * 7 + col
                        let isPresent = [5, 6, 7, 12, 13, 14, 19, 20].contains(dateNum)
                        let isAbsent = [11, 21].contains(dateNum)
                        
                        Text("\(dateNum)")
                            .font(.custom("Tajawal-Bold", size: 12))
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                            .aspectRatio(1, contentMode: .fit)
                            .background(isPresent ? BrandColors.passGreen.opacity(0.15) : (isAbsent ? BrandColors.warningHigh.opacity(0.15) : Color.clear))
                            .foregroundColor(isPresent ? BrandColors.passGreen : (isAbsent ? BrandColors.warningHigh : (isDark ? .white : .black)))
                            .clipShape(Circle())
                    }
                }
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
    
    // Screen 9: Teacher Notes View
    private func teacherNotesView() -> some View {
        VStack(spacing: 0) {
            subViewTopBar(title: "ملاحظات المدرسين")
            
            ScrollView {
                VStack(spacing: 12) {
                    noteCard(teacher: "أ. أحمد سعيد (اللغة العربية)", note: "أحمد طالب ممتاز ومنتبه في الحصة، يحتاج فقط للتركيز على تنظيم الخط.")
                    noteCard(teacher: "أ. محمد خالد (الرياضيات)", note: "مستوى رائع في الفهم والاستيعاب الرياضي وسرعة حل التمارين.")
                    noteCard(teacher: "أ. سارة جمال (العلوم)", note: "يظهر اهتماماً متزايداً بالجانب العملي في مادة الفيزياء والكيمياء.")
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
        VStack(spacing: 0) {
            subViewTopBar(title: "المصاريف والمدفوعات")
            
            VStack(spacing: 16) {
                VStack(spacing: 8) {
                    Text("إجمالي المصاريف الدراسية")
                        .font(.custom("Tajawal-Regular", size: 13))
                        .foregroundColor(.gray)
                    Text("12,000 ج.م")
                        .font(.custom("Tajawal-Bold", size: 28))
                        .fontWeight(.black)
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                }
                .frame(maxWidth: .infinity)
                .padding()
                .background(isDark ? BrandColors.darkCard : .white)
                .cornerRadius(16)
                
                VStack(spacing: 12) {
                    feeDetailRow(label: "المدفوع", value: "8,000 ج.م", isCompleted: true)
                    feeDetailRow(label: "المتبقي", value: "4,000 ج.م", isCompleted: false)
                }
                
                Spacer()
                
                Button(action: {}) {
                    HStack {
                        Image(systemName: "square.and.arrow.down.fill")
                        Text("عرض وتحميل إيصال الدفع")
                    }
                    .font(.custom("Tajawal-Bold", size: 15))
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity)
                    .padding()
                    .background(BrandColors.teal)
                    .cornerRadius(12)
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
                    notificationRow(title: "تنبيه غياب", desc: "غياب الطالب يوم الأحد 5 مايو في مادة الرياضيات الشاملة.", date: "منذ ساعة", color: BrandColors.warningHigh)
                    notificationRow(title: "تنبيه امتحان", desc: "تم جدولة اختبار مادة العلوم العامة يوم الثلاثاء 14 مايو.", date: "منذ ساعتين", color: BrandColors.teal)
                    notificationRow(title: "رسالة من الإدارة الأكاديمية", desc: "يرجى سداد القسط المتبقي من المصاريف الدراسية قبل نهاية الأسبوع.", date: "أمس", color: BrandColors.warmGold)
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
                Text(date)
                    .font(.custom("Tajawal-Regular", size: 10))
                    .foregroundColor(.gray.opacity(0.6))
            }
            Spacer()
        }
        .padding()
        .background(isDark ? BrandColors.darkCard : .white)
        .cornerRadius(12)
    }
    
    // Screen 12: App Settings View
    private func settingsView() -> some View {
        VStack(spacing: 0) {
            subViewTopBar(title: "الإعدادات")
            
            VStack(spacing: 12) {
                settingsRow(title: "تغيير رقم الموبايل", iconName: "phone.fill")
                settingsRow(title: "الدعم الفني والشكاوى", iconName: "wrench.and.screwdriver.fill")
                settingsRow(title: "الأسئلة الشائعة FAQ", iconName: "questionmark.circle.fill")
                settingsRow(title: "عن التطبيق", iconName: "info.circle.fill")
                
                Spacer()
                    .frame(height: 24)
                
                // Logout
                Button(action: {
                    activeSubScreen = nil
                    viewModel.switchProfile(nil) // clear active student
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
