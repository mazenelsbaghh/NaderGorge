import Foundation

public struct StudentProfile: Codable, Identifiable, Equatable {
    public var id: UUID { studentId }
    public let studentId: UUID
    public let name: String
    public let token: String
    
    public init(studentId: UUID, name: String, token: String) {
        self.studentId = studentId
        self.name = name
        self.token = token
    }
}

public struct VerifyCodeRequest: Codable {
    public let trackingCode: String
    public let deviceToken: String
    public let platform: String
    
    public init(trackingCode: String, deviceToken: String, platform: String = "ios") {
        self.trackingCode = trackingCode
        self.deviceToken = deviceToken
        self.platform = platform
    }
}

public struct VerifyCodeResponse: Codable {
    public let token: String
    public let studentName: String
    
    public init(token: String, studentName: String) {
        self.token = token
        self.studentName = studentName
    }
}

public struct StudentDetailsResponse: Codable, Equatable {
    public let studentName: String
    public let grade: String
    public let school: String
    public let avatarSlug: String?
    public let attendance: AttendanceSummary
    public let exams: [ExamDetails]
    public let homeworks: [HomeworkDetails]
    public let warnings: [WarningDetails]
    
    public init(studentName: String, grade: String, school: String, avatarSlug: String?, attendance: AttendanceSummary, exams: [ExamDetails], homeworks: [HomeworkDetails], warnings: [WarningDetails]) {
        self.studentName = studentName
        self.grade = grade
        self.school = school
        self.avatarSlug = avatarSlug
        self.attendance = attendance
        self.exams = exams
        self.homeworks = homeworks
        self.warnings = warnings
    }
}

public struct AttendanceSummary: Codable, Equatable {
    public let totalLessons: Int
    public let watchedLessons: Int
    public let completionRate: Double
    
    public init(totalLessons: Int, watchedLessons: Int, completionRate: Double) {
        self.totalLessons = totalLessons
        self.watchedLessons = watchedLessons
        self.completionRate = completionRate
    }
}

public struct ExamDetails: Codable, Identifiable, Equatable {
    public var id: String { examTitle + "\(submittedAt)" }
    public let examTitle: String
    public let score: Int
    public let totalScore: Int
    public let percentage: Double
    public let submittedAt: String
    public let status: String // "Passed" / "Failed"
    
    public init(examTitle: String, score: Int, totalScore: Int, percentage: Double, submittedAt: String, status: String) {
        self.examTitle = examTitle
        self.score = score
        self.totalScore = totalScore
        self.percentage = percentage
        self.submittedAt = submittedAt
        self.status = status
    }
}

public struct HomeworkDetails: Codable, Identifiable, Equatable {
    public var id: String { title + "\(submittedAt ?? "")" }
    public let title: String
    public let isSubmitted: Bool
    public let grade: String?
    public let submittedAt: String?
    
    public init(title: String, isSubmitted: Bool, grade: String?, submittedAt: String?) {
        self.title = title
        self.isSubmitted = isSubmitted
        self.grade = grade
        self.submittedAt = submittedAt
    }
}

public struct WarningDetails: Codable, Identifiable, Equatable {
    public var id: String { reason + "\(createdAt)" }
    public let reason: String
    public let severity: String // "High" / "Medium" / "Low"
    public let createdAt: String
    
    public init(reason: String, severity: String, createdAt: String) {
        self.reason = reason
        self.severity = severity
        self.createdAt = createdAt
    }
}
