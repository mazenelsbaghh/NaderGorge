import SwiftUI

public struct OnboardingView: View {
    public var onStartTracking: () -> Void
    
    public init(onStartTracking: @escaping () -> Void) {
        self.onStartTracking = onStartTracking
    }
    
    public var body: some View {
        ZStack {
            Color.white
                .ignoresSafeArea()
            
            // Dotted Grid Metaphor Canvas
            GeometryReader { geometry in
                Path { path in
                    let step: CGFloat = 40
                    for x in stride(from: 0, to: geometry.size.width, by: step) {
                        for y in stride(from: 0, to: geometry.size.height, by: step) {
                            path.addEllipse(in: CGRect(x: x, y: y, width: 2, height: 2))
                        }
                    }
                }
                .fill(BrandColors.teal.opacity(0.12))
            }
            .ignoresSafeArea()
            
            // Ascending stairs custom path drawing in background
            GeometryReader { geometry in
                Path { path in
                    let w = geometry.size.width
                    let h = geometry.size.height
                    
                    path.move(to: CGPoint(x: w * 0.1, y: h * 0.8))
                    path.addLine(to: CGPoint(x: w * 0.3, y: h * 0.8))
                    path.addLine(to: CGPoint(x: w * 0.3, y: h * 0.65))
                    path.addLine(to: CGPoint(x: w * 0.5, y: h * 0.65))
                    path.addLine(to: CGPoint(x: w * 0.5, y: h * 0.5))
                    path.addLine(to: CGPoint(x: w * 0.7, y: h * 0.5))
                    path.addLine(to: CGPoint(x: w * 0.7, y: h * 0.35))
                    path.addLine(to: CGPoint(x: w * 0.9, y: h * 0.35))
                }
                .stroke(BrandColors.teal.opacity(0.35), lineWidth: 6)
                
                // Ascent highlights (achievement indicators)
                Circle()
                    .fill(BrandColors.warmGold)
                    .frame(width: 14, height: 14)
                    .position(x: geometry.size.width * 0.3, y: geometry.size.height * 0.65)
                
                Circle()
                    .fill(BrandColors.warmGold)
                    .frame(width: 14, height: 14)
                    .position(x: geometry.size.width * 0.5, y: geometry.size.height * 0.5)
                
                Circle()
                    .fill(BrandColors.warmGold)
                    .frame(width: 20, height: 20)
                    .position(x: geometry.size.width * 0.7, y: geometry.size.height * 0.35)
                
                // Glowing progress spline
                Path { path in
                    let w = geometry.size.width
                    let h = geometry.size.height
                    path.move(to: CGPoint(x: w * 0.1, y: h * 0.8))
                    path.addQuadCurve(to: CGPoint(x: w * 0.7, y: h * 0.35), control: CGPoint(x: w * 0.4, y: h * 0.75))
                }
                .stroke(
                    LinearGradient(colors: [BrandColors.teal, BrandColors.teal.opacity(0.3)], startPoint: .leading, endPoint: .trailing),
                    lineWidth: 3
                )
            }
            
            // Core Text Content & CTAs
            VStack(alignment: .leading, spacing: 0) {
                // Top Branding text
                Text("منصة مسار")
                    .font(.custom("Tajawal-Bold", size: 20))
                    .fontWeight(.black)
                    .foregroundColor(BrandColors.teal)
                    .padding(.top, 24)
                    .padding(.horizontal, 24)
                
                Spacer()
                
                // Large Middle-Left Title & Desc
                VStack(alignment: .leading, spacing: 14) {
                    Text("تابع مستوى\nابنك الأكاديمي\nفي مكان واحد")
                        .font(.custom("Tajawal-Bold", size: 34))
                        .fontWeight(.black)
                        .foregroundColor(BrandColors.deepNavy)
                        .lineSpacing(6)
                        .multilineTextAlignment(.leading)
                    
                    Text("رحلة تعليمية تبدأ بخطوة. تابع الدرجات والحضور والتنبيهات الإرشادية لحظة بلحظة وبشكل فوري.")
                        .font(.custom("Tajawal-Regular", size: 14))
                        .foregroundColor(BrandColors.deepNavy.opacity(0.7))
                        .lineSpacing(4)
                        .multilineTextAlignment(.leading)
                }
                .padding(.horizontal, 24)
                .padding(.bottom, 64)
                
                // Bottom control pill
                Button(action: {
                    withAnimation(.spring(response: 0.4, dampingFraction: 0.8)) {
                        onStartTracking()
                    }
                }) {
                    HStack {
                        // Muted back indicator
                        Image(systemName: "chevron.left")
                            .font(.system(size: 14, weight: .bold))
                            .foregroundColor(.white.opacity(0.4))
                            .frame(width: 48, height: 48)
                            .background(Color.white.opacity(0.1))
                            .clipShape(Circle())
                        
                        Spacer()
                        
                        // Play button and start text
                        HStack(spacing: 12) {
                            Image(systemName: "play.fill")
                                .font(.system(size: 14, weight: .bold))
                                .foregroundColor(.white)
                                .frame(width: 44, height: 44)
                                .background(BrandColors.teal)
                                .clipShape(Circle())
                            
                            Text("ابدأ الآن")
                                .font(.custom("Tajawal-Bold", size: 16))
                                .fontWeight(.bold)
                                .foregroundColor(.white)
                        }
                        
                        Spacer()
                        
                        // Slide dots decoration
                        Text("•••")
                            .font(.system(size: 14, weight: .bold))
                            .foregroundColor(.white.opacity(0.4))
                            .padding(.trailing, 16)
                    }
                    .padding(.horizontal, 8)
                    .frame(height: 64)
                    .background(BrandColors.deepNavy)
                    .clipShape(Capsule())
                }
                .padding(.horizontal, 24)
                .padding(.bottom, 32)
            }
        }
        .environment(\.layoutDirection, .rightToLeft) // Set RTL layout context
    }
}
