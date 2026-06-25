import SwiftUI
import CoreGraphics
import CoreText

@MainActor
public struct ParentAppContainer: View {
    @State private var hasLinkedStudent: Bool = false
    @State private var showOnboarding: Bool = true
    @State private var showSplash: Bool = true
    @StateObject private var dashboardViewModel = DashboardViewModel()
    
    public init() {
        FontRegistrar.registerAllFonts()
    }
    
    public var body: some View {
        Group {
            if showSplash {
                SplashScreenView(onFinished: {
                    withAnimation(.spring()) {
                        showSplash = false
                    }
                })
            } else if hasLinkedStudent {
                DashboardView(viewModel: dashboardViewModel, onAddStudent: {
                    withAnimation(.spring()) {
                        hasLinkedStudent = false
                        showOnboarding = true
                    }
                })
            } else if showOnboarding {
                OnboardingView(onStartTracking: {
                    withAnimation(.spring()) {
                        showOnboarding = false
                    }
                })
            } else {
                LinkingView(onLinkSuccess: {
                    dashboardViewModel.loadProfiles()
                    withAnimation(.spring()) {
                        hasLinkedStudent = !dashboardViewModel.linkedProfiles.isEmpty
                        if hasLinkedStudent {
                            showOnboarding = false
                        }
                    }
                })
            }
        }
        .onAppear {
            dashboardViewModel.loadProfiles()
            let linked = !dashboardViewModel.linkedProfiles.isEmpty
            hasLinkedStudent = linked
            showOnboarding = !linked
            // If they are linked, skip onboarding but keep splash
        }
    }
}

private struct FontRegistrar {
    static func registerFont(named name: String) {
        guard let url = Bundle.module.url(forResource: name, withExtension: "ttf") ??
                        Bundle.module.url(forResource: "Fonts/\(name)", withExtension: "ttf"),
              let data = try? Data(contentsOf: url),
              let provider = CGDataProvider(data: data as CFData),
              let font = CGFont(provider) else {
            return
        }
        var error: Unmanaged<CFError>?
        CTFontManagerRegisterGraphicsFont(font, &error)
    }
    
    static func registerAllFonts() {
        registerFont(named: "Tajawal-Regular")
        registerFont(named: "Tajawal-Medium")
        registerFont(named: "Tajawal-Bold")
        registerFont(named: "Tajawal-Black")
    }
}
