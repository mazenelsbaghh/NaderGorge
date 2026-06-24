import Foundation

public protocol APIServiceProtocol {
    func verifyCode(trackingCode: String, deviceToken: String) async throws -> VerifyCodeResponse
    func fetchStudentDetails(token: String) async throws -> StudentDetailsResponse
}

public class APIService: APIServiceProtocol {
    public static let shared = APIService()
    private let baseURL: URL
    private let session: URLSession
    
    public init(baseURL: URL = URL(string: "http://localhost:5000")!, session: URLSession = .shared) {
        self.baseURL = baseURL
        self.session = session
    }
    
    public func verifyCode(trackingCode: String, deviceToken: String) async throws -> VerifyCodeResponse {
        let url = baseURL.appendingPathComponent("api/parent/verify-code")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let body = VerifyCodeRequest(trackingCode: trackingCode, deviceToken: deviceToken)
        request.httpBody = try JSONEncoder().encode(body)
        
        let (data, response) = try await session.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIError.invalidResponse
        }
        
        guard httpResponse.statusCode == 200 else {
            if httpResponse.statusCode == 400 || httpResponse.statusCode == 404 {
                throw APIError.invalidCode
            }
            throw APIError.serverError(statusCode: httpResponse.statusCode)
        }
        
        return try JSONDecoder().decode(VerifyCodeResponse.self, from: data)
    }
    
    public func fetchStudentDetails(token: String) async throws -> StudentDetailsResponse {
        let url = baseURL.appendingPathComponent("api/parent/student-details")
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        let (data, response) = try await session.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIError.invalidResponse
        }
        
        guard httpResponse.statusCode == 200 else {
            if httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.serverError(statusCode: httpResponse.statusCode)
        }
        
        return try JSONDecoder().decode(StudentDetailsResponse.self, from: data)
    }
}

public enum APIError: Error, LocalizedError, Equatable {
    case invalidResponse
    case invalidCode
    case unauthorized
    case serverError(statusCode: Int)
    
    public var errorDescription: String? {
        switch self {
        case .invalidResponse:
            return "استجابة غير صالحة من الخادم."
        case .invalidCode:
            return "الرمز غير صالح، يرجى التحقق وإعادة المحاولة."
        case .unauthorized:
            return "انتهت صلاحية الجلسة، يرجى إعادة ربط الطالب."
        case .serverError(let code):
            return "خطأ في الخادم (رمز الخطأ: \(code))"
        }
    }
}

public struct JWTDecoder {
    public static func decodeStudentId(from token: String) -> UUID? {
        let parts = token.components(separatedBy: ".")
        guard parts.count == 3 else { return nil }
        
        var base64 = parts[1]
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        
        let remainder = base64.count % 4
        if remainder > 0 {
            base64.append(String(repeating: "=", count: 4 - remainder))
        }
        
        guard let data = Data(base64Encoded: base64) else { return nil }
        
        do {
            if let json = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] {
                if let studentIdStr = json["StudentId"] as? String ?? json["studentId"] as? String {
                    return UUID(uuidString: studentIdStr)
                }
            }
        } catch {
            print("Failed to decode JWT payload: \(error)")
        }
        return nil
    }
}
