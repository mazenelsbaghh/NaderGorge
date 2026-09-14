import SwiftUI
import CoreGraphics
import CoreText

public enum ParentRefreshEvents {
    public static let studentDataChanged = Notification.Name("NaderGorgeParent.studentDataChanged")
    public static let deviceTokenChanged = Notification.Name("NaderGorgeParent.deviceTokenChanged")
}

public enum ParentDeviceTokenStore {
    private static let key = "NaderGorgeParent.apnsDeviceToken"

    public static var token: String? {
        UserDefaults.standard.string(forKey: key)
    }

    public static func save(_ token: String) {
        UserDefaults.standard.set(token, forKey: key)
    }
}

@MainActor
public struct ParentAppContainer: View {
    @State private var hasLinkedStudent: Bool = false
    @State private var showOnboarding: Bool = true
    @State private var showSplash: Bool = true
    @StateObject private var dashboardViewModel = DashboardViewModel()
    @Environment(\.scenePhase) private var scenePhase
    
    public init() {
        FontRegistrar.registerAllFonts()
    }
    
    public var body: some View {
        Group {
            if dashboardViewModel.appConfig.updateRequired {
                UpdateRequiredView(config: dashboardViewModel.appConfig)
            } else if showSplash {
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
                LinkingView(
                    onLinkSuccess: {
                        dashboardViewModel.loadProfiles()
                        withAnimation(.spring()) {
                            hasLinkedStudent = !dashboardViewModel.linkedProfiles.isEmpty
                            if hasLinkedStudent {
                                showOnboarding = false
                            }
                        }
                    },
                    onBack: {
                        withAnimation(.spring()) {
                            if !dashboardViewModel.linkedProfiles.isEmpty {
                                hasLinkedStudent = true
                            } else {
                                showOnboarding = true
                            }
                        }
                    }
                )
            }
        }
        .onAppear {
            dashboardViewModel.loadProfiles()
            let linked = !dashboardViewModel.linkedProfiles.isEmpty
            hasLinkedStudent = linked
            showOnboarding = !linked
            Task {
                await dashboardViewModel.loadAppConfig()
                await dashboardViewModel.registerDeviceToken(ParentDeviceTokenStore.token)
            }
        }
        .onChange(of: scenePhase) { _, phase in
            guard phase == .active else { return }
            Task {
                await dashboardViewModel.refreshActiveStudent()
                await dashboardViewModel.registerDeviceToken(ParentDeviceTokenStore.token)
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: ParentRefreshEvents.studentDataChanged)) { _ in
            Task { await dashboardViewModel.refreshActiveStudent() }
        }
        .onReceive(NotificationCenter.default.publisher(for: ParentRefreshEvents.deviceTokenChanged)) { _ in
            Task { await dashboardViewModel.registerDeviceToken(ParentDeviceTokenStore.token) }
        }
    }
}

private struct UpdateRequiredView: View {
    let config: ParentAppConfig
    @Environment(\.openURL) private var openURL

    var body: some View {
        ZStack {
            BrandColors.offWhite.ignoresSafeArea()
            VStack(spacing: 20) {
                MassarLogoView(isDarkBg: false, showText: true)
                Image(systemName: "arrow.down.app.fill")
                    .font(.system(size: 56))
                    .foregroundColor(BrandColors.teal)
                Text("تحديث مطلوب")
                    .font(.custom("Tajawal-Black", size: 24))
                    .foregroundColor(BrandColors.deepNavy)
                Text(config.updateMessage)
                    .font(.custom("Tajawal-Regular", size: 15))
                    .multilineTextAlignment(.center)
                    .foregroundColor(BrandColors.darkGray)
                    .padding(.horizontal, 24)

                if let url = URL(string: config.updateUrl), !config.updateUrl.isEmpty {
                    Button("تحديث التطبيق") {
                        openURL(url)
                    }
                    .font(.custom("Tajawal-Bold", size: 15))
                    .foregroundColor(.white)
                    .padding(.horizontal, 28)
                    .padding(.vertical, 13)
                    .background(BrandColors.teal)
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                }
            }
            .padding(24)
        }
        .environment(\.layoutDirection, .rightToLeft)
    }
}

private struct FontRegistrar {
    private static var resourceBundle: Bundle {
        #if SWIFT_PACKAGE
        return .module
        #else
        return .main
        #endif
    }

    static func registerFont(named name: String) {
        guard let url = resourceBundle.url(forResource: name, withExtension: "ttf") ??
                        resourceBundle.url(forResource: "Fonts/\(name)", withExtension: "ttf"),
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
