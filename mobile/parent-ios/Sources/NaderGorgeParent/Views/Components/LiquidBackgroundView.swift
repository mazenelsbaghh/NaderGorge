import SwiftUI

public struct LiquidBackgroundView: View {
    @State private var animateGradients = false
    
    public init() {}
    
    public var body: some View {
        ZStack {
            BrandColors.darkBackground
                .ignoresSafeArea()
            
            GeometryReader { geometry in
                ZStack {
                    Circle()
                        .fill(LinearGradient(colors: [BrandColors.deepNavy.opacity(0.4), BrandColors.teal.opacity(0.15)], startPoint: .topLeading, endPoint: .bottomTrailing))
                        .frame(width: geometry.size.width * 0.8, height: geometry.size.width * 0.8)
                        .offset(x: animateGradients ? -50 : 50, y: animateGradients ? -100 : 100)
                        .blur(radius: 60)
                    
                    Circle()
                        .fill(LinearGradient(colors: [BrandColors.teal.opacity(0.2), BrandColors.deepNavy.opacity(0.2)], startPoint: .bottomLeading, endPoint: .topTrailing))
                        .frame(width: geometry.size.width * 0.7, height: geometry.size.width * 0.7)
                        .offset(x: animateGradients ? 100 : -100, y: animateGradients ? 50 : -50)
                        .blur(radius: 50)
                    
                    Circle()
                        .fill(LinearGradient(colors: [BrandColors.teal.opacity(0.15), BrandColors.deepNavy.opacity(0.1)], startPoint: .top, endPoint: .bottom))
                        .frame(width: geometry.size.width * 0.6, height: geometry.size.width * 0.6)
                        .offset(x: animateGradients ? -80 : 80, y: animateGradients ? 120 : -120)
                        .blur(radius: 40)
                }
                .onAppear {
                    withAnimation(.easeInOut(duration: 8.0).repeatForever(autoreverses: true)) {
                        animateGradients.toggle()
                    }
                }
            }
        }
    }
}

