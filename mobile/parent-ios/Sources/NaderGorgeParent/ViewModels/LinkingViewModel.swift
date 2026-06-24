import Foundation
import Combine

@MainActor
public class LinkingViewModel: ObservableObject {
    @Published public var trackingCode: String = ""
    @Published public var isLoading: Bool = false
    @Published public var errorMessage: String? = nil
    @Published public var successMessage: String? = nil
    @Published public var linkedStudentName: String? = nil
    
    private let apiService: APIServiceProtocol
    private let keychainService: KeychainService
    
    public init(apiService: APIServiceProtocol = APIService.shared, keychainService: KeychainService = KeychainService.shared) {
        self.apiService = apiService
        self.keychainService = keychainService
    }
    
    public func linkStudent(deviceToken: String = "MOCK_DEVICE_TOKEN") async {
        errorMessage = nil
        successMessage = nil
        linkedStudentName = nil
        
        let trimmedCode = trackingCode.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        guard trimmedCode.count == 6 else {
            errorMessage = "الرمز غير صالح، يجب أن يتكون من 6 أحرف."
            return
        }
        
        isLoading = true
        defer { isLoading = false }
        
        do {
            let response = try await apiService.verifyCode(trackingCode: trimmedCode, deviceToken: deviceToken)
            
            guard let studentId = JWTDecoder.decodeStudentId(from: response.token) else {
                errorMessage = "رمز التوثيق المستلم غير صالح."
                return
            }
            
            let profile = StudentProfile(studentId: studentId, name: response.studentName, token: response.token)
            try keychainService.addProfile(profile)
            
            linkedStudentName = response.studentName
            successMessage = "تم ربط الطالب \(response.studentName) بنجاح!"
            trackingCode = ""
        } catch let error as APIError {
            errorMessage = error.localizedDescription
        } catch {
            errorMessage = "حدث خطأ غير متوقع: \(error.localizedDescription)"
        }
    }
}
