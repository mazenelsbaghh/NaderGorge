import XCTest
import Combine
import Foundation
@testable import NaderGorgeParent

class NaderGorgeParentTests: XCTestCase {
    
    var mockAPI: MockAPIService!
    var keychain: KeychainService!
    
    override func setUp() {
        super.setUp()
        mockAPI = MockAPIService()
        keychain = KeychainService(useFallback: true)
    }
    
    override func tearDown() {
        try? keychain.clear()
        mockAPI = nil
        keychain = nil
        super.tearDown()
    }
    
    // MARK: - Keychain Tests
    
    func testKeychainAddAndLoad() throws {
        let profile = StudentProfile(
            studentId: UUID(uuidString: "E621E1F8-C36C-495A-93FC-0C247A3E6E5F")!,
            name: "أحمد محمد",
            token: "mock.token.here"
        )
        
        try keychain.addProfile(profile)
        
        let loaded = keychain.loadProfiles()
        XCTAssertEqual(loaded.count, 1)
        XCTAssertEqual(loaded.first?.name, "أحمد محمد")
        XCTAssertEqual(loaded.first?.studentId, profile.studentId)
    }
    
    func testKeychainRemove() throws {
        let studentId = UUID(uuidString: "E621E1F8-C36C-495A-93FC-0C247A3E6E5F")!
        let profile1 = StudentProfile(studentId: studentId, name: "أحمد محمد", token: "token1")
        let profile2 = StudentProfile(studentId: UUID(), name: "سعيد علي", token: "token2")
        
        try keychain.addProfile(profile1)
        try keychain.addProfile(profile2)
        
        XCTAssertEqual(keychain.loadProfiles().count, 2)
        
        try keychain.removeProfile(studentId: studentId)
        
        let loaded = keychain.loadProfiles()
        XCTAssertEqual(loaded.count, 1)
        XCTAssertEqual(loaded.first?.name, "سعيد علي")
    }
    
    // MARK: - LinkingViewModel Tests
    
    @MainActor
    func testLinkStudentInvalidCode() async {
        let viewModel = LinkingViewModel(apiService: mockAPI, keychainService: keychain)
        viewModel.trackingCode = "123"
        
        await viewModel.linkStudent()
        
        XCTAssertFalse(viewModel.isLoading)
        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertEqual(viewModel.errorMessage, "الرمز غير صالح، يجب أن يتكون من 6 أرقام.")
        XCTAssertNil(viewModel.successMessage)
    }
    
    @MainActor
    func testLinkStudentSuccess() async {
        let viewModel = LinkingViewModel(apiService: mockAPI, keychainService: keychain)
        let mockToken = "xxxx.eyJTdHVkZW50SWQiOiAiRTYyMUUxRjgtQzM2Qy00OTVBLTkzRkMtMEMyNDdBM0U2RTVGIn0.yyyy"
        let response = VerifyCodeResponse(token: mockToken, studentName: "أحمد محمد")
        mockAPI.verifyCodeResult = .success(response)
        
        let mockDetails = StudentDetailsResponse(
            studentName: "أحمد محمد",
            grade: "الصف الثالث الثانوي",
            school: "مدرسة الأورمان",
            avatarSlug: "avatar-lion",
            attendance: AttendanceSummary(totalLessons: 10, watchedLessons: 8, completionRate: 80.0),
            exams: [],
            homeworks: [],
            warnings: []
        )
        mockAPI.fetchStudentDetailsResult = .success(mockDetails)
        
        viewModel.trackingCode = "123456"
        await viewModel.linkStudent()
        
        XCTAssertNil(viewModel.errorMessage)
        
        if case .review(let student, let details) = viewModel.uiState {
            XCTAssertEqual(student.name, "أحمد محمد")
            XCTAssertEqual(details.studentName, "أحمد محمد")
            
            viewModel.confirmLink(student: student)
            
            XCTAssertEqual(viewModel.linkedStudentName, "أحمد محمد")
            XCTAssertEqual(viewModel.successMessage, "تم ربط الطالب أحمد محمد بنجاح!")
            XCTAssertEqual(viewModel.trackingCode, "")
            
            let loaded = keychain.loadProfiles()
            XCTAssertEqual(loaded.count, 1)
            XCTAssertEqual(loaded.first?.name, "أحمد محمد")
            XCTAssertEqual(loaded.first?.studentId, UUID(uuidString: "E621E1F8-C36C-495A-93FC-0C247A3E6E5F"))
        } else {
            XCTAssertTrue(false, "Expected .review state, but got \(viewModel.uiState)")
        }
    }
    
    @MainActor
    func testLinkStudentAPIFailure() async {
        let viewModel = LinkingViewModel(apiService: mockAPI, keychainService: keychain)
        mockAPI.verifyCodeResult = .failure(APIError.invalidCode)
        
        viewModel.trackingCode = "123456"
        await viewModel.linkStudent()
        
        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertEqual(viewModel.errorMessage, APIError.invalidCode.localizedDescription)
        XCTAssertNil(viewModel.successMessage)
    }
    
    // MARK: - DashboardViewModel Tests
    
    @MainActor
    func testDashboardLoadProfilesAndFetchDetails() async throws {
        let studentId = UUID(uuidString: "E621E1F8-C36C-495A-93FC-0C247A3E6E5F")!
        let profile = StudentProfile(studentId: studentId, name: "أحمد محمد", token: "token1")
        try keychain.addProfile(profile)
        
        let mockDetails = StudentDetailsResponse(
            studentName: "أحمد محمد",
            grade: "الصف الثالث الثانوي",
            school: "مدرسة الأورمان",
            avatarSlug: "avatar-lion",
            attendance: AttendanceSummary(totalLessons: 10, watchedLessons: 8, completionRate: 80.0),
            exams: [ExamDetails(examTitle: "اختبار الكيمياء", score: 45, totalScore: 50, percentage: 90.0, submittedAt: "2026-06-20", status: "Passed")],
            homeworks: [HomeworkDetails(title: "الواجب الأول", isSubmitted: true, grade: "ممتاز", submittedAt: "2026-06-21")],
            warnings: []
        )
        mockAPI.fetchStudentDetailsResult = .success(mockDetails)
        
        let viewModel = DashboardViewModel(apiService: mockAPI, keychainService: keychain)
        
        XCTAssertEqual(viewModel.linkedProfiles.count, 1)
        XCTAssertEqual(viewModel.selectedProfile?.studentId, studentId)
        
        await viewModel.fetchDetails()
        
        XCTAssertFalse(viewModel.isLoading)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNotNil(viewModel.studentDetails)
        XCTAssertEqual(viewModel.studentDetails?.studentName, "أحمد محمد")
        XCTAssertEqual(viewModel.studentDetails?.attendance.completionRate, 80.0)
    }
    
    @MainActor
    func testDashboardRemoveProfile() async throws {
        let profile1 = StudentProfile(studentId: UUID(), name: "طالب 1", token: "token1")
        let profile2 = StudentProfile(studentId: UUID(), name: "طالب 2", token: "token2")
        try keychain.addProfile(profile1)
        try keychain.addProfile(profile2)
        
        let viewModel = DashboardViewModel(apiService: mockAPI, keychainService: keychain)
        XCTAssertEqual(viewModel.linkedProfiles.count, 2)
        
        let initialSelected = viewModel.selectedProfile
        XCTAssertNotNil(initialSelected)
        
        await viewModel.removeProfile(initialSelected!)
        
        XCTAssertEqual(viewModel.linkedProfiles.count, 1)
        XCTAssertNotEqual(viewModel.selectedProfile?.studentId, initialSelected?.studentId)
    }
}

// MARK: - Mocks

class MockAPIService: APIServiceProtocol {
    var verifyCodeResult: Result<VerifyCodeResponse, Error>?
    var fetchStudentDetailsResult: Result<StudentDetailsResponse, Error>?
    
    func verifyCode(trackingCode: String, deviceToken: String) async throws -> VerifyCodeResponse {
        guard let result = verifyCodeResult else {
            throw APIError.invalidResponse
        }
        switch result {
        case .success(let response):
            return response
        case .failure(let error):
            throw error
        }
    }
    
    func fetchStudentDetails(token: String) async throws -> StudentDetailsResponse {
        guard let result = fetchStudentDetailsResult else {
            throw APIError.invalidResponse
        }
        switch result {
        case .success(let response):
            return response
        case .failure(let error):
            throw error
        }
    }
}
