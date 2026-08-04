import Foundation

public protocol APIServiceProtocol {
    func verifyCode(trackingCode: String, deviceToken: String) async throws -> VerifyCodeResponse
    func fetchStudentDetails(token: String) async throws -> StudentDetailsResponse
    func fetchNotifications(token: String) async throws -> [ParentNotification]
    func markNotificationAsRead(token: String, notificationId: String) async throws
    func registerDeviceToken(token: String, deviceToken: String) async throws
    func fetchAppConfig() async throws -> ParentAppConfig
}

public class APIService: APIServiceProtocol {
    public static let shared = APIService()
    private let baseURL: URL
    private let session: URLSession
    
    public init(baseURL: URL = URL(string: "https://api.massar-academy.net")!, session: URLSession = .shared) {
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
        
        return try decodeApiResponse(
            VerifyCodeResponse.self,
            from: data,
            statusCode: httpResponse.statusCode,
            invalidCodeStatuses: [400, 404]
        )
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
        
        return try decodeApiResponse(
            StudentDetailsResponse.self,
            from: data,
            statusCode: httpResponse.statusCode,
            unauthorizedStatuses: [401, 403]
        )
    }

    public func fetchNotifications(token: String) async throws -> [ParentNotification] {
        let url = baseURL.appendingPathComponent("api/parent/notifications")
        var request = URLRequest(url: url)
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else { throw APIError.invalidResponse }
        return try decodeApiResponse(ParentNotificationList.self, from: data, statusCode: httpResponse.statusCode).items
    }

    public func markNotificationAsRead(token: String, notificationId: String) async throws {
        let url = baseURL.appendingPathComponent("api/parent/notifications/\(notificationId)/read")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else { throw APIError.invalidResponse }
        _ = try decodeApiResponse(Bool.self, from: data, statusCode: httpResponse.statusCode)
    }

    public func registerDeviceToken(token: String, deviceToken: String) async throws {
        let url = baseURL.appendingPathComponent("api/parent/device-token")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONEncoder().encode(RegisterDeviceTokenRequest(deviceToken: deviceToken))
        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else { throw APIError.invalidResponse }
        _ = try decodeApiResponse(Bool.self, from: data, statusCode: httpResponse.statusCode)
    }

    public func fetchAppConfig() async throws -> ParentAppConfig {
        let url = baseURL.appendingPathComponent("api/parent/app-config")
        let (data, response) = try await session.data(from: url)
        guard let httpResponse = response as? HTTPURLResponse else { throw APIError.invalidResponse }
        return try decodeApiResponse(ParentAppConfig.self, from: data, statusCode: httpResponse.statusCode)
    }

    private func decodeApiResponse<T: Decodable>(
        _ type: T.Type,
        from data: Data,
        statusCode: Int,
        invalidCodeStatuses: Set<Int> = [],
        unauthorizedStatuses: Set<Int> = []
    ) throws -> T {
        let decoder = JSONDecoder()
        let envelope = try? decoder.decode(ApiResponse<T>.self, from: data)

        guard (200..<300).contains(statusCode) else {
            if invalidCodeStatuses.contains(statusCode) {
                throw APIError.invalidCode
            }
            if unauthorizedStatuses.contains(statusCode) {
                throw APIError.unauthorized
            }
            if let message = envelope?.message, !message.isEmpty {
                throw APIError.apiMessage(message)
            }
            throw APIError.serverError(statusCode: statusCode)
        }

        if let envelope {
            guard envelope.success, let payload = envelope.data else {
                throw APIError.apiMessage(envelope.message ?? "استجابة غير مكتملة من الخادم.")
            }
            return payload
        }

        return try decoder.decode(T.self, from: data)
    }
}

public enum APIError: Error, LocalizedError, Equatable {
    case invalidResponse
    case invalidCode
    case unauthorized
    case apiMessage(String)
    case serverError(statusCode: Int)
    
    public var errorDescription: String? {
        switch self {
        case .invalidResponse:
            return "استجابة غير صالحة من الخادم."
        case .invalidCode:
            return "الرمز غير صالح، يرجى التحقق وإعادة المحاولة."
        case .unauthorized:
            return "انتهت صلاحية الجلسة، يرجى إعادة ربط الطالب."
        case .apiMessage(let message):
            return message
        case .serverError(let code):
            return "خطأ في الخادم (رمز الخطأ: \(code))"
        }
    }
}

private struct ApiResponse<T: Decodable>: Decodable {
    let success: Bool
    let data: T?
    let message: String?
    let errors: [String]?
}

private struct ParentNotificationList: Decodable {
    let items: [ParentNotification]

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        items = (try? container.decode([ParentNotification].self)) ?? []
    }
}

public struct JWTDecoder {
    public static func decodeStudentId(from token: String) -> String? {
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
                if let studentId = json["StudentId"] as? String ?? json["studentId"] as? String ?? json["sub"] as? String {
                    return studentId
                }
            }
        } catch {
            print("Failed to decode JWT payload: \(error)")
        }
        return nil
    }
}
