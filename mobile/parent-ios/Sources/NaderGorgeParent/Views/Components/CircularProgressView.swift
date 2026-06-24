import SwiftUI

public struct CircularProgressView: View {
    public var progress: Double
    public var title: String
    public var subtitle: String
    public var progressColor: Color = .cyan
    
    public init(progress: Double, title: String, subtitle: String, progressColor: Color = .cyan) {
        self.progress = progress
        self.title = title
        self.subtitle = subtitle
        self.progressColor = progressColor
    }
    
    public var body: some View {
        VStack(spacing: 8) {
            ZStack {
                Circle()
                    .stroke(Color.white.opacity(0.1), lineWidth: 12)
                    
                Circle()
                    .trim(from: 0.0, to: CGFloat(min(self.progress, 1.0)))
                    .stroke(
                        AngularGradient(
                            colors: [progressColor, progressColor.opacity(0.7), progressColor],
                            center: .center
                        ),
                        style: StrokeStyle(lineWidth: 12, lineCap: .round, lineJoin: .round)
                    )
                    .rotationEffect(Angle(degrees: -90))
                    .animation(.spring(response: 0.8, dampingFraction: 0.6), value: progress)
                
                VStack(spacing: 2) {
                    Text(title)
                        .font(.system(.title3, design: .rounded))
                        .fontWeight(.bold)
                        .foregroundColor(.white)
                    
                    Text(subtitle)
                        .font(.system(.caption, design: .rounded))
                        .foregroundColor(.white.opacity(0.6))
                }
            }
            .frame(width: 120, height: 120)
        }
    }
}
