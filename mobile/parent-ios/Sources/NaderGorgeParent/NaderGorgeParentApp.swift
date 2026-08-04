import SwiftUI
import UIKit
import UserNotifications

@main
struct NaderGorgeParentApp: App {
    @UIApplicationDelegateAdaptor(ParentAppDelegate.self) private var appDelegate

    var body: some Scene {
        WindowGroup {
            ParentAppContainer()
        }
    }
}

final class ParentAppDelegate: NSObject, UIApplicationDelegate, UNUserNotificationCenterDelegate {
    func application(
        _ application: UIApplication,
        didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil
    ) -> Bool {
        UNUserNotificationCenter.current().delegate = self
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .badge, .sound]) { granted, _ in
            guard granted else { return }
            DispatchQueue.main.async { application.registerForRemoteNotifications() }
        }
        return true
    }

    func applicationDidBecomeActive(_ application: UIApplication) {
        // Re-register on every foreground transition so a newly issued APNs token
        // is delivered after an app restore, reinstall, or provisioning change.
        application.registerForRemoteNotifications()
    }

    func application(_ application: UIApplication, didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        let token = deviceToken.map { String(format: "%02x", $0) }.joined()
        ParentDeviceTokenStore.save(token)
        NotificationCenter.default.post(name: ParentRefreshEvents.deviceTokenChanged, object: token)
    }

    func application(_ application: UIApplication, didFailToRegisterForRemoteNotificationsWithError error: Error) {
        NSLog("Parent APNs registration failed: %@", error.localizedDescription)
    }

    func application(_ application: UIApplication, didReceiveRemoteNotification userInfo: [AnyHashable: Any]) {
        NotificationCenter.default.post(name: ParentRefreshEvents.studentDataChanged, object: userInfo["studentId"] as? String)
    }

    func application(
        _ application: UIApplication,
        didReceiveRemoteNotification userInfo: [AnyHashable: Any],
        fetchCompletionHandler completionHandler: @escaping (UIBackgroundFetchResult) -> Void
    ) {
        NotificationCenter.default.post(name: ParentRefreshEvents.studentDataChanged, object: userInfo["studentId"] as? String)
        completionHandler(.newData)
    }

    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        NotificationCenter.default.post(name: ParentRefreshEvents.studentDataChanged, object: notification.request.content.userInfo["studentId"] as? String)
        completionHandler([.banner, .sound, .badge])
    }
}
