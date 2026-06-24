import SwiftUI

@MainActor
public struct DashboardView: View {
    @StateObject private var viewModel: DashboardViewModel
    @State private var selectedTab: Tab = .overview
    public var onAddStudent: () -> Void
    
    public enum Tab: String, CaseIterable {
        case overview = "نظرة عامة"
        case lectures = "المحاضرات"
        case grades = "الدرجات والإنذارات"
    }
    
    public init(viewModel: DashboardViewModel? = nil, onAddStudent: @escaping () -> Void) {
        self._viewModel = StateObject(wrappedValue: viewModel ?? DashboardViewModel())
        self.onAddStudent = onAddStudent
    }
    
    public var body: some View {
        ZStack {
            LiquidBackgroundView()
                .ignoresSafeArea()
            
            VStack(spacing: 0) {
                topNavigationBar
                    .padding()
                
                if viewModel.isLoading {
                    Spacer()
                    ProgressView()
                        .tint(.white)
                        .scaleEffect(1.5)
                    Spacer()
                } else if let details = viewModel.studentDetails {
                    tabSelector
                        .padding(.horizontal)
                        .padding(.bottom, 12)
                    
                    ScrollView {
                        VStack(spacing: 20) {
                            switch selectedTab {
                            case .overview:
                                overviewTab(details: details)
                            case .lectures:
                                lecturesTab(details: details)
                            case .grades:
                                gradesTab(details: details)
                            }
                        }
                        .padding()
                    }
                    .transition(.opacity)
                } else {
                    Spacer()
                    VStack(spacing: 16) {
                        Image(systemName: "person.crop.circle.badge.exclamationmark")
                            .font(.system(size: 50))
                            .foregroundColor(.white.opacity(0.6))
                        Text("لم يتم العثور على بيانات الطالب.")
                            .foregroundColor(.white.opacity(0.8))
                        Button("تحديث البيانات") {
                            Task {
                                await viewModel.fetchDetails()
                            }
                        }
                        .padding(.horizontal, 24)
                        .padding(.vertical, 10)
                        .background(Color.white.opacity(0.15))
                        .cornerRadius(10)
                    }
                    Spacer()
                }
            }
        }
        .environment(\.layoutDirection, .rightToLeft)
        .onAppear {
            Task {
                await viewModel.fetchDetails()
            }
        }
    }
    
    private var topNavigationBar: some View {
        HStack {
            if let selected = viewModel.selectedProfile {
                Button(action: {
                    Task {
                        await viewModel.removeProfile(selected)
                    }
                }) {
                    Image(systemName: "person.fill.xmark")
                        .foregroundColor(.red.opacity(0.8))
                        .padding(10)
                        .background(Color.red.opacity(0.1))
                        .clipShape(Circle())
                }
            }
            
            Spacer()
            
            if !viewModel.linkedProfiles.isEmpty {
                Menu {
                    ForEach(viewModel.linkedProfiles) { profile in
                        Button(action: {
                            Task {
                                await viewModel.selectProfile(profile)
                            }
                        }) {
                            HStack {
                                if viewModel.selectedProfile?.studentId == profile.studentId {
                                    Image(systemName: "checkmark")
                                }
                                Text(profile.name)
                            }
                        }
                    }
                    
                    Divider()
                    
                    Button(action: onAddStudent) {
                        Label("ربط طالب جديد", systemImage: "plus")
                    }
                } label: {
                    HStack(spacing: 8) {
                        Image(systemName: "chevron.down")
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.7))
                        Text(viewModel.selectedProfile?.name ?? "اختر طالب")
                            .font(.headline)
                            .foregroundColor(.white)
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 8)
                    .background(Color.white.opacity(0.1))
                    .cornerRadius(20)
                    .overlay(
                        RoundedRectangle(cornerRadius: 20)
                            .stroke(Color.white.opacity(0.15), lineWidth: 1)
                    )
                }
            } else {
                Button("ربط طالب جديد", action: onAddStudent)
                    .foregroundColor(.cyan)
                    .font(.headline)
            }
            
            Spacer()
            
            Button(action: onAddStudent) {
                Image(systemName: "plus")
                    .foregroundColor(.white)
                    .padding(10)
                    .background(Color.white.opacity(0.1))
                    .clipShape(Circle())
            }
        }
    }
    
    private var tabSelector: some View {
        HStack(spacing: 0) {
            ForEach(Tab.allCases, id: \.self) { tab in
                Button(action: {
                    withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                        selectedTab = tab
                    }
                }) {
                    Text(tab.rawValue)
                        .font(.subheadline)
                        .fontWeight(.bold)
                        .foregroundColor(selectedTab == tab ? .black : .white)
                        .padding(.vertical, 10)
                        .frame(maxWidth: .infinity)
                        .background(
                            ZStack {
                                if selectedTab == tab {
                                    Color.cyan
                                        .cornerRadius(10)
                                        .matchedGeometryEffect(id: "activeTab", in: tabAnimationNamespace)
                                }
                            }
                        )
                }
            }
        }
        .padding(4)
        .background(Color.white.opacity(0.08))
        .cornerRadius(12)
        .overlay(
            RoundedRectangle(cornerRadius: 12)
                .stroke(Color.white.opacity(0.1), lineWidth: 0.8)
        )
    }
    
    @Namespace private var tabAnimationNamespace
    
    private func overviewTab(details: StudentDetailsResponse) -> some View {
        VStack(spacing: 20) {
            HStack(spacing: 16) {
                ZStack {
                    Circle()
                        .fill(Color.white.opacity(0.1))
                        .frame(width: 70, height: 70)
                    Image(systemName: "person.circle.fill")
                        .font(.system(size: 60))
                        .foregroundColor(.cyan.opacity(0.8))
                }
                
                VStack(alignment: .trailing, spacing: 4) {
                    Text(details.studentName)
                        .font(.title2)
                        .fontWeight(.bold)
                        .foregroundColor(.white)
                    
                    Text(details.grade)
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.7))
                    
                    Text(details.school)
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.5))
                }
                Spacer()
            }
            .glassCard()
            
            VStack(spacing: 16) {
                Text("تقرير حضور المحاضرات")
                    .font(.headline)
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity, alignment: .trailing)
                
                HStack(spacing: 30) {
                    CircularProgressView(
                        progress: details.attendance.completionRate / 100.0,
                        title: "\(Int(details.attendance.completionRate))%",
                        subtitle: "نسبة الحضور",
                        progressColor: .cyan
                    )
                    
                    VStack(alignment: .trailing, spacing: 12) {
                        HStack {
                            Text("المحاضرات الكلية:")
                                .foregroundColor(.white.opacity(0.6))
                            Spacer()
                            Text("\(details.attendance.totalLessons)")
                                .fontWeight(.bold)
                                .foregroundColor(.white)
                        }
                        
                        HStack {
                            Text("المحاضرات المكتملة:")
                                .foregroundColor(.white.opacity(0.6))
                            Spacer()
                            Text("\(details.attendance.watchedLessons)")
                                .fontWeight(.bold)
                                .foregroundColor(.green)
                        }
                        
                        HStack {
                            Text("محاضرات متبقية:")
                                .foregroundColor(.white.opacity(0.6))
                            Spacer()
                            Text("\(details.attendance.totalLessons - details.attendance.watchedLessons)")
                                .fontWeight(.bold)
                                .foregroundColor(.orange)
                        }
                    }
                    .font(.subheadline)
                }
            }
            .glassCard()
            
            if let warning = details.warnings.first {
                HStack(spacing: 12) {
                    Image(systemName: "exclamationmark.triangle.fill")
                        .foregroundColor(.red)
                        .font(.title3)
                    
                    VStack(alignment: .trailing, spacing: 2) {
                        Text("آخر إنذار أكاديمي")
                            .font(.caption)
                            .fontWeight(.bold)
                            .foregroundColor(.red)
                        Text(warning.reason)
                            .font(.footnote)
                            .foregroundColor(.white)
                            .lineLimit(1)
                    }
                    Spacer()
                }
                .padding()
                .background(Color.red.opacity(0.15))
                .cornerRadius(12)
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .stroke(Color.red.opacity(0.3), lineWidth: 1)
                )
            }
        }
    }
    
    private func lecturesTab(details: StudentDetailsResponse) -> some View {
        VStack(spacing: 16) {
            Text("قائمة واجبات ومحاضرات الطالب")
                .font(.headline)
                .foregroundColor(.white)
                .frame(maxWidth: .infinity, alignment: .trailing)
            
            if details.homeworks.isEmpty {
                Text("لا توجد واجبات مسجلة حالياً.")
                    .font(.subheadline)
                    .foregroundColor(.white.opacity(0.5))
                    .padding()
            } else {
                ForEach(details.homeworks) { hw in
                    HStack(spacing: 12) {
                        ZStack {
                            Circle()
                                .fill(hw.isSubmitted ? Color.green.opacity(0.15) : Color.orange.opacity(0.15))
                                .frame(width: 44, height: 44)
                            Image(systemName: hw.isSubmitted ? "doc.plaintext.fill" : "doc.plaintext")
                                .foregroundColor(hw.isSubmitted ? .green : .orange)
                        }
                        
                        VStack(alignment: .trailing, spacing: 4) {
                            Text(hw.title)
                                .font(.subheadline)
                                .fontWeight(.semibold)
                                .foregroundColor(.white)
                            
                            if let submittedAt = hw.submittedAt {
                                Text("تاريخ التسليم: \(submittedAt)")
                                    .font(.caption2)
                                    .foregroundColor(.white.opacity(0.5))
                            } else {
                                Text("لم يتم التسليم بعد")
                                    .font(.caption2)
                                    .foregroundColor(.orange)
                            }
                        }
                        
                        Spacer()
                        
                        if hw.isSubmitted {
                            Text(hw.grade ?? "مقبول")
                                .font(.footnote)
                                .fontWeight(.bold)
                                .foregroundColor(.green)
                                .padding(.horizontal, 10)
                                .padding(.vertical, 4)
                                .background(Color.green.opacity(0.1))
                                .cornerRadius(8)
                        } else {
                            Text("معلق")
                                .font(.footnote)
                                .fontWeight(.bold)
                                .foregroundColor(.orange)
                                .padding(.horizontal, 10)
                                .padding(.vertical, 4)
                                .background(Color.orange.opacity(0.1))
                                .cornerRadius(8)
                        }
                    }
                    .glassCard()
                }
            }
        }
    }
    
    private func gradesTab(details: StudentDetailsResponse) -> some View {
        VStack(spacing: 24) {
            VStack(spacing: 12) {
                Text("درجات الاختبارات")
                    .font(.headline)
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity, alignment: .trailing)
                
                if details.exams.isEmpty {
                    Text("لا توجد اختبارات مسجلة.")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.5))
                        .padding()
                } else {
                    ForEach(details.exams) { exam in
                        HStack {
                            VStack(alignment: .trailing, spacing: 4) {
                                Text(exam.examTitle)
                                    .font(.subheadline)
                                    .fontWeight(.bold)
                                    .foregroundColor(.white)
                                Text("النتيجة: \(exam.score) / \(exam.totalScore)")
                                    .font(.footnote)
                                    .foregroundColor(.white.opacity(0.7))
                            }
                            
                            Spacer()
                            
                            VStack(alignment: .leading, spacing: 4) {
                                Text("\(Int(exam.percentage))%")
                                    .font(.title3)
                                    .fontWeight(.bold)
                                    .foregroundColor(exam.status == "Passed" ? .green : .red)
                                
                                Text(exam.status == "Passed" ? "ناجح" : "غير مجتاز")
                                    .font(.caption2)
                                    .foregroundColor(exam.status == "Passed" ? .green : .red)
                            }
                        }
                        .glassCard()
                    }
                }
            }
            
            VStack(spacing: 12) {
                Text("الإنذارات الأكاديمية والتنبيهات")
                    .font(.headline)
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity, alignment: .trailing)
                
                if details.warnings.isEmpty {
                    Text("سجل الطالب خالي من الإنذارات.")
                        .font(.subheadline)
                        .foregroundColor(.green.opacity(0.8))
                        .padding()
                        .frame(maxWidth: .infinity)
                        .background(Color.green.opacity(0.1))
                        .cornerRadius(12)
                } else {
                    ForEach(details.warnings) { warning in
                        HStack(alignment: .top, spacing: 12) {
                            Image(systemName: "exclamationmark.triangle.fill")
                                .foregroundColor(warning.severity == "High" ? .red : (warning.severity == "Medium" ? .orange : .yellow))
                                .font(.title3)
                            
                            VStack(alignment: .trailing, spacing: 4) {
                                Text(warning.reason)
                                    .font(.footnote)
                                    .foregroundColor(.white)
                                    .multilineTextAlignment(.trailing)
                                
                                Text("التاريخ: \(warning.createdAt)")
                                    .font(.caption2)
                                    .foregroundColor(.white.opacity(0.5))
                            }
                            Spacer()
                        }
                        .padding()
                        .background(Color.white.opacity(0.05))
                        .cornerRadius(12)
                        .overlay(
                            RoundedRectangle(cornerRadius: 12)
                                .stroke(Color.white.opacity(0.1), lineWidth: 1)
                        )
                    }
                }
            }
        }
    }
}
