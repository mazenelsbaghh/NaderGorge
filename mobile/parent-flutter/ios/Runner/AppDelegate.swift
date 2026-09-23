import Flutter
import UIKit
import Security
import UserNotifications

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
    private var deviceChannel: FlutterMethodChannel?
    private var apnsToken: String?
    override func application(_ application: UIApplication, didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        UNUserNotificationCenter.current().delegate = self
        application.registerForRemoteNotifications()
        return super.application(application, didFinishLaunchingWithOptions: launchOptions)
    }
    func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
        GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)
        let registrar = engineBridge.pluginRegistry.registrar(forPlugin: "MassarDeviceBridge")!
        let channel = FlutterMethodChannel(name: "net.massaracademy.parent/device", binaryMessenger: registrar.messenger())
        deviceChannel = channel
        channel.setMethodCallHandler { [weak self] call, result in
            switch call.method {
            case "legacyProfiles": self?.readLegacy(result)
            case "deviceToken": result(self?.apnsToken)
            case "requestNotifications":
                UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .badge, .sound]) { granted, error in
                    DispatchQueue.main.async {
                        if error != nil { result(FlutterError(code: "push", message: "Notification permission unavailable", details: nil)); return }
                        if granted { UIApplication.shared.registerForRemoteNotifications() }
                        result(granted)
                    }
                }
            case "openSettings":
                UIApplication.shared.open(URL(string: UIApplication.openSettingsURLString)!) { opened in
                    if opened { result(nil) } else { result(FlutterError(code: "settings", message: "Unable to open settings", details: nil)) }
                }
            default: result(FlutterMethodNotImplemented)
            }
        }
    }
    private func readLegacy(_ result: FlutterResult) {
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: "com.nadergorge.parent", kSecAttrAccount as String: "NaderGorgeParentProfiles",
            kSecReturnData as String: true, kSecMatchLimit as String: kSecMatchLimitOne]
        var reference: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &reference)
        if status == errSecItemNotFound { result([String: String]()); return }
        guard status == errSecSuccess, let bytes = reference as? Data, let profiles = String(data: bytes, encoding: .utf8) else {
            result(FlutterError(code: "migration", message: "Unable to read legacy profiles", details: nil)); return
        }
        var payload: [String: Any] = ["profiles": profiles]
        if let activeId = UserDefaults.standard.string(forKey: "NaderGorgeParentActiveStudentId") { payload["activeId"] = activeId }
        result(payload)
    }
    override func application(_ application: UIApplication, didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        apnsToken = deviceToken.map { String(format: "%02x", $0) }.joined()
        deviceChannel?.invokeMethod("tokenChanged", arguments: nil)
        super.application(application, didRegisterForRemoteNotificationsWithDeviceToken: deviceToken)
    }
    override func application(_ application: UIApplication, didReceiveRemoteNotification userInfo: [AnyHashable: Any], fetchCompletionHandler completionHandler: @escaping (UIBackgroundFetchResult) -> Void) {
        deviceChannel?.invokeMethod("refresh", arguments: nil)
        completionHandler(.newData)
    }
    override func userNotificationCenter(_ center: UNUserNotificationCenter, willPresent notification: UNNotification, withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        deviceChannel?.invokeMethod("refresh", arguments: nil)
        completionHandler([.banner, .sound, .badge])
    }
    override func userNotificationCenter(_ center: UNUserNotificationCenter, didReceive response: UNNotificationResponse, withCompletionHandler completionHandler: @escaping () -> Void) {
        deviceChannel?.invokeMethod("refresh", arguments: nil)
        completionHandler()
    }
}
