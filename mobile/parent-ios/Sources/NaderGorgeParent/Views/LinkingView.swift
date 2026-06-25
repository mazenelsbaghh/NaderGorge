import SwiftUI

public struct MassarLogoView: View {
    public var isDarkBg: Bool = false
    public var showText: Bool = true
    
    public init(isDarkBg: Bool = false, showText: Bool = true) {
        self.isDarkBg = isDarkBg
        self.showText = showText
    }
    
    public var body: some View {
        HStack(spacing: 10) {
            Image(isDarkBg ? "logo-mark-light" : "logo-mark", bundle: .module)
                .resizable()
                .aspectRatio(contentMode: .fit)
                .frame(width: 36, height: 36)
            
            if showText {
                VStack(alignment: .leading, spacing: 1) {
                    Text("مسار")
                        .font(.custom("Tajawal-Bold", size: 18))
                        .fontWeight(.black)
                        .foregroundColor(isDarkBg ? .white : BrandColors.deepNavy)
                    
                    Text("أكاديمي")
                        .font(.custom("Tajawal-Bold", size: 11))
                        .fontWeight(.bold)
                        .foregroundColor(BrandColors.teal)
                }
            }
        }
    }
}

public struct CornerDotsView: View {
    public var body: some View {
        Canvas { context, size in
            let spacing: CGFloat = 10
            for r in 0..<4 {
                for c in 0..<4 {
                    let rect = CGRect(x: CGFloat(c) * spacing, y: CGFloat(r) * spacing, width: 3, height: 3)
                    context.fill(Path(ellipseIn: rect), with: .color(BrandColors.warmGold.opacity(0.4)))
                }
            }
        }
        .frame(width: 40, height: 40)
    }
}

@MainActor
public struct LinkingView: View {
    @StateObject private var viewModel: LinkingViewModel
    public var onLinkSuccess: () -> Void
    
    @State private var rememberMe: Bool = true
    @Environment(\.colorScheme) var colorScheme
    
    public init(viewModel: LinkingViewModel? = nil, onLinkSuccess: @escaping () -> Void) {
        self._viewModel = StateObject(wrappedValue: viewModel ?? LinkingViewModel())
        self.onLinkSuccess = onLinkSuccess
    }
    
    private var isDark: Bool {
        colorScheme == .dark
    }
    
    public var body: some View {
        Group {
            switch viewModel.uiState {
            case .review(let student, let details):
                StudentConfirmationView(
                    student: student,
                    details: details,
                    onConfirm: {
                        viewModel.confirmLink(student: student)
                        onLinkSuccess()
                    },
                    onCancel: {
                        viewModel.cancelLink()
                    }
                )
            default:
                VStack(spacing: 0) {
                    // 1. Curved Top Header Card
                    ZStack {
                        // Background Dot Pattern Mockup
                        VStack {
                            HStack {
                                CornerDotsLightView()
                                    .padding(16)
                                Spacer()
                                CornerDotsLightView()
                                    .padding(16)
                            }
                            Spacer()
                        }
                        
                        VStack(spacing: 12) {
                            // Circle housing the brand SVG logo
                            ZStack {
                                Circle()
                                    .fill(Color.white.opacity(0.15))
                                    .frame(width: 72, height: 72)
                                
                                Image("logo-mark-light", bundle: .module)
                                    .resizable()
                                    .aspectRatio(contentMode: .fit)
                                    .frame(width: 44, height: 44)
                            }
                            
                            Text("بوابة المتابعة")
                                .font(.custom("Tajawal-Bold", size: 22))
                                .fontWeight(.black)
                                .foregroundColor(.white)
                            
                            Text("نظام متابعة الطلاب الأكاديمي")
                                .font(.custom("Tajawal-Medium", size: 12))
                                .foregroundColor(.white.opacity(0.8))
                        }
                    }
                    .frame(maxWidth: .infinity)
                    .frame(height: 220)
                    .background(BrandColors.teal)
                    .clipShape(UnevenRoundedRectangle(bottomLeadingRadius: 32, bottomTrailingRadius: 32))
                    
                    // 2. Main Content Box (Form + Keypad)
                    ScrollView {
                        VStack(spacing: 16) {
                            VStack(spacing: 16) {
                                Text("تسجيل الدخول - عصري")
                                    .font(.custom("Tajawal-Bold", size: 18))
                                    .fontWeight(.black)
                                    .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                                
                                Text("يرجى إدخال رقم المتابعة الخاص بالطالب")
                                    .font(.custom("Tajawal-Medium", size: 13))
                                    .foregroundColor(.gray)
                                
                                // Display Input Field
                                HStack {
                                    Image(systemName: "person.fill")
                                        .foregroundColor(BrandColors.teal)
                                        .font(.system(size: 20))
                                        .frame(width: 24)
                                    
                                    Spacer()
                                        .frame(width: 16)
                                    
                                    Text(viewModel.trackingCode.isEmpty ? "مثال: 123456" : viewModel.trackingCode)
                                        .font(.custom("Tajawal-Bold", size: 18))
                                        .fontWeight(.bold)
                                        .tracking(viewModel.trackingCode.isEmpty ? 0 : 4)
                                        .foregroundColor(viewModel.trackingCode.isEmpty ? .gray.opacity(0.7) : (isDark ? .white : BrandColors.deepNavy))
                                    
                                    Spacer()
                                }
                                .padding()
                                .background(isDark ? BrandColors.darkBackground : BrandColors.softGray.opacity(0.4))
                                .cornerRadius(12)
                                
                                // Custom Keypad
                                numericKeypad
                                
                                if let error = viewModel.errorMessage {
                                    Text(error)
                                        .font(.custom("Tajawal-Regular", size: 12))
                                        .foregroundColor(.red)
                                        .frame(maxWidth: .infinity, alignment: .center)
                                }
                                
                                // Login Action Button
                                Button(action: {
                                    if viewModel.errorMessage != nil {
                                        viewModel.cancelLink()
                                    } else {
                                        Task {
                                            await viewModel.linkStudent()
                                        }
                                    }
                                }) {
                                    HStack {
                                        if viewModel.isLoading {
                                            ProgressView()
                                                .tint(.white)
                                                .padding(.trailing, 8)
                                        }
                                        Text(viewModel.errorMessage != nil ? "إعادة المحاولة" : "تأكيد المتابعة")
                                            .font(.custom("Tajawal-Bold", size: 16))
                                            .fontWeight(.bold)
                                        
                                        Image(systemName: viewModel.errorMessage != nil ? "arrow.counterclockwise" : "arrow.right.to.line")
                                            .font(.system(size: 16, weight: .bold))
                                    }
                                    .foregroundColor(.white)
                                    .frame(maxWidth: .infinity)
                                    .padding()
                                    .background(viewModel.errorMessage != nil ? Color.red : BrandColors.teal)
                                    .cornerRadius(12)
                                    .shadow(color: (viewModel.errorMessage != nil ? Color.red : BrandColors.teal).opacity(0.3), radius: 8, x: 0, y: 4)
                                }
                                .disabled(viewModel.isLoading || (viewModel.trackingCode.count != 6 && viewModel.errorMessage == nil))
                                .opacity((viewModel.trackingCode.count == 6 || viewModel.errorMessage != nil) ? 1.0 : 0.6)
                                
                                Text("أو")
                                    .font(.custom("Tajawal-Bold", size: 13))
                                    .foregroundColor(.gray)
                                
                                // QR Scanner Button
                                Button(action: { /* Mock scan action */ }) {
                                    HStack {
                                        Text("Scan QR Code")
                                            .font(.custom("Tajawal-Bold", size: 16))
                                            .fontWeight(.bold)
                                        
                                        Image(systemName: "qrcode.viewfinder")
                                            .font(.system(size: 18))
                                    }
                                    .foregroundColor(BrandColors.teal)
                                    .frame(maxWidth: .infinity)
                                    .padding()
                                    .background(Color.clear)
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 12)
                                            .stroke(BrandColors.teal, lineWidth: 2)
                                    )
                                }
                            }
                            .padding(24)
                            .background(isDark ? BrandColors.darkCard : .white)
                            .cornerRadius(20)
                            .shadow(color: Color.black.opacity(isDark ? 0.3 : 0.08), radius: 15, x: 0, y: 10)
                            .padding(.horizontal, 20)
                            
                            // Support & Copyright footer
                            VStack(spacing: 8) {
                                Text("تواجه مشكلة في الدخول؟")
                                    .font(.custom("Tajawal-Regular", size: 13))
                                    .foregroundColor(.gray)
                                
                                Button(action: {}) {
                                    Text("تواصل مع الدعم الفني")
                                        .font(.custom("Tajawal-Bold", size: 13))
                                        .fontWeight(.bold)
                                        .foregroundColor(BrandColors.teal)
                                }
                                
                                Spacer()
                                    .frame(height: 12)
                                
                                Text("© ٢٠٢٤ جميع الحقوق محفوظة لشركة حلول التعليم")
                                    .font(.custom("Tajawal-Regular", size: 11))
                                    .foregroundColor(.gray.opacity(0.7))
                            }
                            .padding(.top, 16)
                            .padding(.bottom, 24)
                        }
                    }
                }
            }
        }
        .environment(\.layoutDirection, .rightToLeft)
    }
    
    private var numericKeypad: some View {
        VStack(spacing: 8) {
            let rows = [
                ["1", "2", "3"],
                ["4", "5", "6"],
                ["7", "8", "9"]
            ]
            
            ForEach(0..<3) { rowIndex in
                HStack(spacing: 8) {
                    ForEach(0..<3) { colIndex in
                        let digit = rows[rowIndex][colIndex]
                        keyButton(label: digit) {
                            if viewModel.trackingCode.count < 6 {
                                viewModel.trackingCode.append(digit)
                            }
                        }
                    }
                }
            }
            
            HStack(spacing: 8) {
                // Clear button
                keyButton(label: "مسح") {
                    viewModel.trackingCode = ""
                }
                
                // Zero button
                keyButton(label: "0") {
                    if viewModel.trackingCode.count < 6 {
                        viewModel.trackingCode.append("0")
                    }
                }
                
                // Backspace button
                keyButton(iconName: "delete.left.fill") {
                    if !viewModel.trackingCode.isEmpty {
                        viewModel.trackingCode.removeLast()
                    }
                }
            }
        }
        .environment(\.layoutDirection, .leftToRight)
    }
    
    private func keyButton(label: String? = nil, iconName: String? = nil, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            ZStack {
                if let label = label {
                    Text(label)
                        .font(.custom("Tajawal-Bold", size: 18))
                        .fontWeight(.bold)
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                } else if let iconName = iconName {
                    Image(systemName: iconName)
                        .font(.system(size: 18, weight: .bold))
                        .foregroundColor(isDark ? .white : BrandColors.deepNavy)
                }
            }
            .frame(maxWidth: .infinity)
            .frame(height: 54)
            .background(isDark ? BrandColors.darkBackground : BrandColors.softGray.opacity(0.4))
            .cornerRadius(12)
        }
    }
}

struct CornerDotsLightView: View {
    var body: some View {
        Canvas { context, size in
            let spacing: CGFloat = 10
            for r in 0..<4 {
                for c in 0..<4 {
                    let rect = CGRect(x: CGFloat(c) * spacing, y: CGFloat(r) * spacing, width: 3, height: 3)
                    context.fill(Path(ellipseIn: rect), with: .color(Color.white.opacity(0.25)))
                }
            }
        }
        .frame(width: 40, height: 40)
    }
}
