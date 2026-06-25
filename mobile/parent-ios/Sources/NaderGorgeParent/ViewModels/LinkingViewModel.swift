import Foundation
import Combine

public enum LinkingUiState: Equatable {
    case idle
    case loading
    case review(student: StudentProfile, details: StudentDetailsResponse)
    case success(studentName: String)
    case error(message: String)
}

@MainActor
public class LinkingViewModel: ObservableObject {
    @Published public var trackingCode: String = "" {
        didSet {
            let filtered = trackingCode.filter { $0.isNumber }
            if filtered != trackingCode {
                trackingCode = filtered
            }
        }
    }
    @Published public var isLoading: Bool = false
    @Published public var errorMessage: String? = nil
    @Published public var successMessage: String? = nil
    @Published public var linkedStudentName: String? = nil
    @Published public var uiState: LinkingUiState = .idle
    
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
        uiState = .loading
        
        let trimmedCode = trackingCode.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmedCode.count == 6 else {
            let errorMsg = "الرمز غير صالح، يجب أن يتكون من 6 أرقام."
            errorMessage = errorMsg
            uiState = .error(message: errorMsg)
            return
        }
        
        isLoading = true
        defer { isLoading = false }
        
        do {
            let response = try await apiService.verifyCode(trackingCode: trimmedCode, deviceToken: deviceToken)
            
            guard let studentId = JWTDecoder.decodeStudentId(from: response.token) else {
                let errorMsg = "رمز التوثيق المستلم غير صالح."
                errorMessage = errorMsg
                uiState = .error(message: errorMsg)
                return
            }
            
            let student = StudentProfile(studentId: studentId, name: response.studentName, token: response.token)
            
            // Phase 2: Fetch student details using the new token BEFORE saving to keychain
            let details = try await apiService.fetchStudentDetails(token: response.token)
            
            uiState = .review(student: student, details: details)
        } catch let error as APIError {
            let errorMsg = error.localizedDescription
            errorMessage = errorMsg
            uiState = .error(message: errorMsg)
        } catch {
            let errorMsg = "حدث خطأ غير متوقع: \(error.localizedDescription)"
            errorMessage = errorMsg
            uiState = .error(message: errorMsg)
        }
    }
    
    public func confirmLink(student: StudentProfile) {
        do {
            try keychainService.addProfile(student)
            linkedStudentName = student.name
            successMessage = "تم ربط الطالب \(student.name) بنجاح!"
            trackingCode = ""
            uiState = .success(studentName: student.name)
        } catch {
            let errorMsg = "فشل في حفظ ملف الطالب."
            errorMessage = errorMsg
            uiState = .error(message: errorMsg)
        }
    }
    
    public func cancelLink() {
        uiState = .idle
        trackingCode = ""
        errorMessage = nil
        successMessage = nil
    }
}
