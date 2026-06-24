import Foundation
import Combine

@MainActor
public class DashboardViewModel: ObservableObject {
    @Published public var linkedProfiles: [StudentProfile] = []
    @Published public var selectedProfile: StudentProfile? = nil
    @Published public var studentDetails: StudentDetailsResponse? = nil
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
        if selectedProfile == nil || !linkedProfiles.contains(where: { $0.studentId == selectedProfile?.studentId }) {
            selectedProfile = linkedProfiles.first
        }
    }
    
    public func selectProfile(_ profile: StudentProfile) async {
        selectedProfile = profile
        await fetchDetails()
    }
    
    public func fetchDetails() async {
        guard let profile = selectedProfile else {
            studentDetails = nil
            return
        }
        
        isLoading = true
        errorMessage = nil
        
        do {
            let details = try await apiService.fetchStudentDetails(token: profile.token)
            studentDetails = details
        } catch let error as APIError {
            errorMessage = error.localizedDescription
        } catch {
            errorMessage = "فشل في تحديث البيانات: \(error.localizedDescription)"
        }
        isLoading = false
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
        } catch {
            errorMessage = "فشل في إزالة ملف الطالب."
        }
    }
}
