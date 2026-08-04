import Foundation
import Combine

@MainActor
public class DashboardViewModel: ObservableObject {
    @Published public var linkedProfiles: [StudentProfile] = []
    @Published public var selectedProfile: StudentProfile? = nil
    @Published public var studentDetails: StudentDetailsResponse? = nil
    @Published public var notifications: [ParentNotification] = []
    @Published public var appConfig = ParentAppConfig()
    @Published public var isLoading: Bool = false
    @Published public var errorMessage: String? = nil
    
    private let apiService: APIServiceProtocol
    private let keychainService: KeychainService
    
    public init(apiService: APIServiceProtocol = APIService.shared, keychainService: KeychainService = KeychainService.shared) {
        self.apiService = apiService
        self.keychainService = keychainService
        loadProfiles()
    }
    
    public func loadProfiles() {
        linkedProfiles = keychainService.loadProfiles()
        let activeProfile = linkedProfiles.first { $0.studentId == keychainService.activeStudentId() }
        if selectedProfile == nil || !linkedProfiles.contains(where: { $0.studentId == selectedProfile?.studentId }) {
            selectedProfile = activeProfile ?? linkedProfiles.first
            keychainService.setActiveStudentId(selectedProfile?.studentId)
        }
    }
    
    public func selectProfile(_ profile: StudentProfile) async {
        selectedProfile = profile
        await fetchDetails()
    }
    
    public func switchProfile(_ profile: StudentProfile?) {
        selectedProfile = profile
        keychainService.setActiveStudentId(profile?.studentId)
        if profile != nil {
            Task {
                await fetchDetails()
            }
        } else {
            studentDetails = nil
        }
    }
    
    public func fetchDetails() async {
        guard let profile = selectedProfile else {
            studentDetails = nil
            return
        }
        
        isLoading = true
        errorMessage = nil
        var didLoadDetails = false
        
        do {
            let details = try await apiService.fetchStudentDetails(token: profile.token)
            studentDetails = details
            didLoadDetails = true
        } catch let error as APIError {
            errorMessage = error.localizedDescription
        } catch is DecodingError {
            errorMessage = "فشل في قراءة بيانات الطالب من الخادم."
        } catch is URLError {
            errorMessage = "تعذر الاتصال بالخادم، حاول مرة أخرى."
        } catch {
            // Unknown SDK/transport failures are surfaced as a safe retry message.
            errorMessage = "فشل في تحديث بيانات الطالب، حاول مرة أخرى."
        }

        guard didLoadDetails else {
            isLoading = false
            return
        }

        do {
            notifications = try await apiService.fetchNotifications(token: profile.token)
        } catch is APIError {
            // Notifications are an optional Android-compatible side channel; keep student details visible.
        } catch is DecodingError {
            // Notifications are an optional Android-compatible side channel; keep student details visible.
        } catch is URLError {
            // Notifications are an optional Android-compatible side channel; keep student details visible.
        } catch {
            // Notifications are an optional Android-compatible side channel; keep student details visible.
        }
        isLoading = false
    }

    public func refreshActiveStudent() async {
        await fetchDetails()
    }

    public func loadAppConfig() async {
        do {
            appConfig = try await apiService.fetchAppConfig()
        } catch is APIError {
            // Configuration is optional at startup; the Android client also keeps the default gate on failure.
        } catch is DecodingError {
            // Configuration is optional at startup; the Android client also keeps the default gate on failure.
        } catch is URLError {
            // Configuration is optional at startup; the Android client also keeps the default gate on failure.
        } catch {
            // Configuration is optional at startup; keep the default configuration on unknown failures.
        }
    }

    public func registerDeviceToken(_ deviceToken: String?) async {
        guard let profile = selectedProfile,
              let deviceToken,
              !deviceToken.isEmpty,
              deviceToken != "ios-parent-pending-token" else { return }
        do {
            try await apiService.registerDeviceToken(token: profile.token, deviceToken: deviceToken)
        } catch is APIError {
            // Registration is retried on the next refresh or token change.
        } catch is DecodingError {
            // Registration is retried on the next refresh or token change.
        } catch is URLError {
            // Registration is retried on the next refresh or token change.
        } catch {
            // Registration is retried on the next refresh or token change.
        }
    }

    public func markNotificationAsRead(_ notification: ParentNotification) async {
        guard let profile = selectedProfile else { return }
        do {
            try await apiService.markNotificationAsRead(token: profile.token, notificationId: notification.id)
        } catch is APIError {
            errorMessage = "تعذر تحديث حالة الإشعار."
            return
        } catch is DecodingError {
            errorMessage = "تعذر تحديث حالة الإشعار."
            return
        } catch is URLError {
            errorMessage = "تعذر تحديث حالة الإشعار."
            return
        } catch {
            errorMessage = "تعذر تحديث حالة الإشعار."
            return
        }

        do {
            notifications = try await apiService.fetchNotifications(token: profile.token)
        } catch is APIError {
            // The read operation succeeded; a refresh failure should not undo it.
        } catch is DecodingError {
            // The read operation succeeded; a refresh failure should not undo it.
        } catch is URLError {
            // The read operation succeeded; a refresh failure should not undo it.
        } catch {
            // The read operation succeeded; a refresh failure should not undo it.
        }
    }
    
    public func removeProfile(_ profile: StudentProfile) async {
        do {
            try keychainService.removeProfile(studentId: profile.studentId)
            loadProfiles()
            if selectedProfile == nil {
                studentDetails = nil
            } else {
                await fetchDetails()
            }
        } catch is KeychainError {
            errorMessage = "فشل في إزالة ملف الطالب."
        } catch {
            errorMessage = "فشل في إزالة ملف الطالب."
        }
    }
}
