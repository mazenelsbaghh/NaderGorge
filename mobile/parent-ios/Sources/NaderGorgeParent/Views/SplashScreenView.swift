import SwiftUI

public struct SplashScreenView: View {
    public var onFinished: () -> Void
    
    @State private var progress: CGFloat = 0.0
    @State private var isFloating = false
    @Environment(\.colorScheme) var colorScheme
    
    public init(onFinished: @escaping () -> Void) {
        self.onFinished = onFinished
    }
    
    private var isDark: Bool {
        colorScheme == .dark
    }
    
    public var body: some View {
        ZStack {
            // Background Gradient Mesh
            LinearGradient(
                colors: [
                    isDark ? BrandColors.darkBackground : BrandColors.offWhite,
                    BrandColors.teal.opacity(0.08),
                    BrandColors.teal.opacity(0.15)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()
            
            // Corner Dots Patterns
            VStack {
                HStack {
                    CornerDotsLightView()
                        .padding(24)
                    Spacer()
                    CornerDotsLightView()
                        .padding(24)
                }
                Spacer()
            }
            .ignoresSafeArea()
            
            VStack(spacing: 0) {
                Spacer()
                
                // Center Branding
                VStack(spacing: 24) {
                    // Floating Logo Box
                    ZStack {
                        RoundedRectangle(cornerRadius: 32)
                            .fill(BrandColors.teal)
                            .frame(width: 128, height: 128)
                            .shadow(color: BrandColors.teal.opacity(0.3), radius: 10, x: 0, y: 5)
                        
                        Image("logo-mark-light", bundle: .module)
                            .resizable()
                            .aspectRatio(contentMode: .fit)
                            .frame(width: 80, height: 80)
                    }
                    .offset(y: isFloating ? -12 : 0)
                    .animation(Animation.easeInOut(duration: 2.0).repeatForever(autoreverses: true), value: isFloating)
                    
                    VStack(spacing: 8) {
                        Text("بوابة المتابعة")
                            .font(.custom("Tajawal-Bold", size: 26))
                            .fontWeight(.black)
                            .foregroundColor(BrandColors.teal)
                        
                        Text("منصة تعليمية متطورة لمتابعة الأداء الأكاديمي والنمو المعرفي")
                            .font(.custom("Tajawal-Regular", size: 14))
                            .foregroundColor(.gray)
                            .multilineTextAlignment(.center)
                            .lineSpacing(4)
                            .padding(.horizontal, 40)
                    }
                }
                
                Spacer()
                
                // Loading Section
                VStack(spacing: 12) {
                    ZStack(alignment: .leading) {
                        Capsule()
                            .fill(Color.gray.opacity(0.2))
                            .frame(width: 250, height: 6)
                        
                        Capsule()
                            .fill(BrandColors.teal)
                            .frame(width: 250 * progress, height: 6)
                    }
                    
                    Text("جاري تحميل البيانات...")
                        .font(.custom("Tajawal-Medium", size: 12))
                        .foregroundColor(.gray.opacity(0.8))
                }
                .padding(.bottom, 48)
            }
        }
        .onAppear {
            isFloating = true
            
            // Animate progress loader over 2 seconds
            withAnimation(.easeInOut(duration: 2.0)) {
                progress = 1.0
            }
            
            // Finish splash after 2.2 seconds
            DispatchQueue.main.asyncAfter(deadline: .now() + 2.2) {
                onFinished()
            }
        }
        .environment(\.layoutDirection, .rightToLeft)
    }
}
