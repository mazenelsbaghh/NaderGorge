import SwiftUI

public struct StudentConfirmationView: View {
    let student: StudentProfile
    let details: StudentDetailsResponse
    let onConfirm: () -> Void
    let onCancel: () -> Void
    
    @Environment(\.colorScheme) var colorScheme
    
    private var isDark: Bool {
        colorScheme == .dark
    }
    
    private var stageName: String {
        if details.grade.localizedCaseInsensitiveContains("Baccalaureate") || details.grade.localizedCaseInsensitiveContains("ثانوي") {
            return "المرحلة الثانوية"
        } else if details.grade.localizedCaseInsensitiveContains("Medium") || details.grade.localizedCaseInsensitiveContains("متوسط") {
            return "المرحلة المتوسطة"
        } else {
            return "المرحلة الدراسية"
        }
    }
    
    public init(
        student: StudentProfile,
        details: StudentDetailsResponse,
        onConfirm: @escaping () -> Void,
        onCancel: @escaping () -> Void
    ) {
        self.student = student
        self.details = details
        self.onConfirm = onConfirm
        self.onCancel = onCancel
    }
    
    public var body: some View {
        VStack(spacing: 0) {
            // 1. Top Bar
            HStack {
                Button(action: onCancel) {
                    Image(systemName: "arrow.right")
                        .font(.system(size: 18, weight: .bold))
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                }
                
                Spacer()
                
                HStack(spacing: 8) {
                    Image(systemName: "person.badge.key.fill")
                        .foregroundColor(BrandColors.teal)
                        .font(.system(size: 16))
                    
                    Text("بوابة المتابعة")
                        .font(.custom("Tajawal-Bold", size: 18))
                        .fontWeight(.bold)
                        .foregroundColor(BrandColors.teal)
                }
                
                Spacer()
                
                // Placeholder to balance navigation bar
                Image(systemName: "arrow.right")
                    .font(.system(size: 18, weight: .bold))
                    .foregroundColor(.clear)
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)
            .background(isDark ? BrandColors.darkCard : .white)
            .shadow(color: Color.black.opacity(isDark ? 0.2 : 0.05), radius: 5, x: 0, y: 2)
            
            // 2. Main content scroll area
            ScrollView {
                VStack(spacing: 24) {
                    // Header title & subtitle
                    VStack(spacing: 6) {
                        Text("تأكيد الربط - مراجعة البيانات")
                            .font(.custom("Tajawal-Bold", size: 20))
                            .fontWeight(.black)
                            .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                            .multilineTextAlignment(.center)
                        
                        Text("يرجى التأكد من هوية الطالب المعروض أدناه لإتمام عملية الربط التعليمي.")
                            .font(.custom("Tajawal-Medium", size: 13))
                            .foregroundColor(.gray)
                            .multilineTextAlignment(.center)
                            .padding(.horizontal, 16)
                    }
                    .padding(.top, 16)
                    
                    // Student Card
                    VStack(spacing: 0) {
                        // Teal Banner with dot patterns
                        ZStack {
                            BrandColors.teal
                            
                            HStack {
                                CornerDotsLightView()
                                    .padding(12)
                                Spacer()
                                CornerDotsLightView()
                                    .padding(12)
                            }
                        }
                        .frame(height: 80)
                        
                        // Avatar, name & details grid
                        VStack(spacing: 0) {
                            // Circular Avatar (overlaps the banner)
                            ZStack {
                                Circle()
                                    .fill(isDark ? BrandColors.darkCard : .white)
                                    .frame(width: 84, height: 84)
                                    .shadow(color: Color.black.opacity(0.1), radius: 4, x: 0, y: 2)
                                
                                Circle()
                                    .fill(BrandColors.teal.opacity(0.1))
                                    .frame(width: 76, height: 76)
                                
                                Text(String(details.studentName.first ?? "ط"))
                                    .font(.custom("Tajawal-Bold", size: 32))
                                    .fontWeight(.bold)
                                    .foregroundColor(BrandColors.teal)
                            }
                            .offset(y: -42)
                            .padding(.bottom, -42)
                            
                            VStack(spacing: 4) {
                                Text(details.studentName)
                                    .font(.custom("Tajawal-Bold", size: 18))
                                    .fontWeight(.black)
                                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                                    .multilineTextAlignment(.center)
                                
                                HStack(spacing: 4) {
                                    Image(systemName: "person.text.rectangle")
                                        .font(.system(size: 13))
                                        .foregroundColor(BrandColors.teal)
                                    
                                    Text("رقم المتابعة: \(student.studentId.uuidString.prefix(8))")
                                        .font(.custom("Tajawal-Medium", size: 13))
                                        .fontWeight(.medium)
                                        .foregroundColor(BrandColors.teal)
                                }
                            }
                            .padding(.top, 16)
                            
                            Divider()
                                .padding(.vertical, 20)
                            
                            // 2x2 Details Grid
                            VStack(spacing: 12) {
                                HStack(spacing: 12) {
                                    DetailCard(label: "المرحلة الدراسية", value: stageName)
                                    DetailCard(label: "الصف الدراسي", value: details.grade)
                                }
                                HStack(spacing: 12) {
                                    DetailCard(label: "الفصل الدراسي", value: "1 / أ")
                                    DetailCard(label: "المدرسة", value: details.school)
                                }
                            }
                        }
                        .padding(.horizontal, 20)
                        .padding(.bottom, 24)
                    }
                    .background(isDark ? BrandColors.darkCard : .white)
                    .cornerRadius(20)
                    .shadow(color: Color.black.opacity(isDark ? 0.3 : 0.08), radius: 12, x: 0, y: 6)
                    .padding(.horizontal, 20)
                    
                    // Warning Disclaimer Box (amber yellow style)
                    HStack(alignment: .top, spacing: 12) {
                        Image(systemName: "info.circle.fill")
                            .foregroundColor(BrandColors.warmGold)
                            .font(.system(size: 18))
                            .padding(.top, 2)
                        
                        Text("بمجرد التأكيد، ستتمكن من الوصول الكامل إلى الدرجات، السلوك، والجدول المدرسي الخاص بالطالب.")
                            .font(.custom("Tajawal-Medium", size: 12))
                            .fontWeight(.medium)
                            .foregroundColor(BrandColors.warmGold)
                            .lineSpacing(4)
                            .multilineTextAlignment(.leading)
                    }
                    .padding(16)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(BrandColors.warmGold.opacity(isDark ? 0.1 : 0.15))
                    .cornerRadius(12)
                    .overlay(
                        RoundedRectangle(cornerRadius: 12)
                            .stroke(BrandColors.warmGold.opacity(0.3), lineWidth: 1)
                    )
                    .padding(.horizontal, 20)
                    
                    // Action Buttons
                    VStack(spacing: 12) {
                        // Confirm Link Button
                        Button(action: onConfirm) {
                            Text("نعم، هذا طفلي")
                                .font(.custom("Tajawal-Bold", size: 16))
                                .fontWeight(.bold)
                                .foregroundColor(.white)
                                .frame(maxWidth: .infinity)
                                .frame(height: 54)
                                .background(BrandColors.teal)
                                .cornerRadius(12)
                                .shadow(color: BrandColors.teal.opacity(0.3), radius: 6, x: 0, y: 3)
                        }
                        
                        // Cancel Link Button
                        Button(action: onCancel) {
                            Text("ليس طفلي / بيانات خاطئة")
                                .font(.custom("Tajawal-Bold", size: 16))
                                .fontWeight(.bold)
                                .foregroundColor(.gray)
                                .frame(maxWidth: .infinity)
                                .frame(height: 54)
                                .background(Color.clear)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 12)
                                        .stroke(Color.gray.opacity(0.5), lineWidth: 1)
                                )
                        }
                    }
                    .padding(.horizontal, 20)
                    .padding(.bottom, 24)
                }
            }
        }
        .background(isDark ? BrandColors.darkBackground : BrandColors.offWhite)
        .environment(\.layoutDirection, .rightToLeft)
    }
}

struct DetailCard: View {
    let label: String
    let value: String
    
    @Environment(\.colorScheme) var colorScheme
    
    private var isDark: Bool {
        colorScheme == .dark
    }
    
    var body: some View {
        VStack(spacing: 4) {
            Text(label)
                .font(.custom("Tajawal-Regular", size: 11))
                .foregroundColor(.gray)
                .multilineTextAlignment(.center)
            
            Text(value)
                .font(.custom("Tajawal-Bold", size: 13))
                .fontWeight(.bold)
                .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 12)
        .padding(.horizontal, 8)
        .background(isDark ? Color.white.opacity(0.04) : BrandColors.softGray.opacity(0.4))
        .cornerRadius(8)
    }
}
