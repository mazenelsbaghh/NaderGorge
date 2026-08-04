import Foundation

#if canImport(UserNotifications)
import UserNotifications
#endif

/// The small platform bridge used by the Settings screen. Keeping it behind
/// conditional imports lets the data layer and Swift package tests build on
/// non-Apple hosts while the app target still exposes the same behaviour as
/// the Android parent app.
public enum ParentNotificationService {
    public static func requestAuthorization() async -> Bool {
        #if canImport(UserNotifications)
        return await withCheckedContinuation { continuation in
            UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .badge, .sound]) { granted, _ in
                continuation.resume(returning: granted)
            }
        }
        #else
        return false
        #endif
    }

    public static func authorizationStatus() async -> Bool {
        #if canImport(UserNotifications)
        return await withCheckedContinuation { continuation in
            UNUserNotificationCenter.current().getNotificationSettings { settings in
                continuation.resume(returning: settings.authorizationStatus == .authorized)
            }
        }
        #else
        return false
        #endif
    }

    @discardableResult
    public static func showTestNotification() async -> Bool {
        #if canImport(UserNotifications)
        let content = UNMutableNotificationContent()
        content.title = "مسار أكاديمي"
        content.body = "الإشعارات تعمل بنجاح على جهازك."
        content.sound = .default
        let request = UNNotificationRequest(
            identifier: "nader-gorge-parent-test-\(UUID().uuidString)",
            content: content,
            trigger: nil
        )
        do {
            try await UNUserNotificationCenter.current().add(request)
            return true
        } catch let error as NSError {
            NSLog("Parent test notification failed: %@", error.localizedDescription)
            return false
        } catch {
            NSLog("Parent test notification failed with an unknown error.")
            return false
        }
        #else
        return false
        #endif
    }
}
