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
    public let studentId: String?
    
    public init(token: String, studentName: String, studentId: String? = nil) {
        self.token = token
        self.studentName = studentName
        self.studentId = studentId
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
    public let teachers: [TeacherSummary]
    public let watchLessons: [WatchLessonDetails]
    public let balance: BalanceDetails?
    public let courses: [CourseSummary]
    
    public init(
        studentName: String,
        grade: String,
        school: String,
        avatarSlug: String?,
        attendance: AttendanceSummary,
        exams: [ExamDetails],
        homeworks: [HomeworkDetails],
        warnings: [WarningDetails],
        teachers: [TeacherSummary] = [],
        watchLessons: [WatchLessonDetails] = [],
        balance: BalanceDetails? = nil,
        courses: [CourseSummary] = []
    ) {
        self.studentName = studentName
        self.grade = grade
        self.school = school
        self.avatarSlug = avatarSlug
        self.attendance = attendance
        self.exams = exams
        self.homeworks = homeworks
        self.warnings = warnings
        self.teachers = teachers
        self.watchLessons = watchLessons
        self.balance = balance
        self.courses = courses
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
    public var id: String { examTitle + (submittedAt ?? "") }
    public let examId: String?
    public let attemptId: String?
    public let packageId: String?
    public let packageName: String?
    public let termId: String?
    public let termTitle: String?
    public let teacherId: String?
    public let teacherName: String?
    public let examTitle: String
    public let score: Double
    public let totalScore: Double
    public let percentage: Double
    public let submittedAt: String?
    public let status: String // "Passed" / "Failed"
    public let mistakes: [QuestionReview]?
    
    public init(
        examId: String? = nil,
        attemptId: String? = nil,
        packageId: String? = nil,
        packageName: String? = nil,
        termId: String? = nil,
        termTitle: String? = nil,
        teacherId: String? = nil,
        teacherName: String? = nil,
        examTitle: String,
        score: Double,
        totalScore: Double,
        percentage: Double,
        submittedAt: String?,
        status: String,
        mistakes: [QuestionReview]? = nil
    ) {
        self.examId = examId
        self.attemptId = attemptId
        self.packageId = packageId
        self.packageName = packageName
        self.termId = termId
        self.termTitle = termTitle
        self.teacherId = teacherId
        self.teacherName = teacherName
        self.examTitle = examTitle
        self.score = score
        self.totalScore = totalScore
        self.percentage = percentage
        self.submittedAt = submittedAt
        self.status = status
        self.mistakes = mistakes
    }
}

public struct HomeworkDetails: Codable, Identifiable, Equatable {
    public var id: String { title + "\(submittedAt ?? "")" }
    public let homeworkId: String?
    public let teacherId: String?
    public let teacherName: String?
    public let title: String
    public let isSubmitted: Bool
    public let submissionState: String?
    public let grade: String?
    public let submittedAt: String?
    public let mistakes: [HomeworkAnswerReview]?
    
    public init(
        homeworkId: String? = nil,
        teacherId: String? = nil,
        teacherName: String? = nil,
        title: String,
        isSubmitted: Bool,
        submissionState: String? = nil,
        grade: String?,
        submittedAt: String?,
        mistakes: [HomeworkAnswerReview]? = nil
    ) {
        self.homeworkId = homeworkId
        self.teacherId = teacherId
        self.teacherName = teacherName
        self.title = title
        self.isSubmitted = isSubmitted
        self.submissionState = submissionState
        self.grade = grade
        self.submittedAt = submittedAt
        self.mistakes = mistakes
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

public struct TeacherSummary: Codable, Identifiable, Equatable {
    public var id: String { teacherId }
    public let teacherId: String
    public let teacherName: String
    public let specialization: String?
    public let profileImageUrl: String?
}

public struct CourseSummary: Codable, Identifiable, Equatable {
    public var id: String { packageId }
    public let packageId: String
    public let packageName: String
    public let teacherId: String
    public let teacherName: String
    public let terms: [CourseTermSummary]
}

public struct CourseTermSummary: Codable, Identifiable, Equatable {
    public var id: String { termId }
    public let termId: String
    public let termTitle: String
    public let lessonCount: Int
    public let examCount: Int
}

public struct WatchLessonDetails: Codable, Identifiable, Equatable {
    public var id: String { lessonId }
    public let packageId: String
    public let packageName: String
    public let termId: String
    public let termTitle: String
    public let teacherId: String
    public let teacherName: String
    public let lessonId: String
    public let lessonTitle: String
    public let totalVideos: Int
    public let watchedVideos: Int
    public let watchCount: Int
    public let watchedSeconds: Int
    public let isCompleted: Bool
    public let lastWatchedAt: String?
}

public struct QuestionReview: Codable, Equatable {
    public let questionText: String
    public let studentAnswer: String?
    public let correctAnswer: String?
    public let writtenCorrection: String?
    public let pointsAwarded: Double
    public let points: Double
}

public struct HomeworkAnswerReview: Codable, Equatable {
    public let questionText: String
    public let studentAnswer: String
    public let correctAnswer: String?
    public let writtenCorrection: String?
    public let scoreReceived: Int?
    public let points: Int
}

public struct BalanceDetails: Codable, Equatable {
    public let currentBalance: Double
    public let transactions: [BalanceTransactionDetails]
}

public struct BalanceTransactionDetails: Codable, Identifiable, Equatable {
    public var id: String { "\(createdAt)-\(amount)-\(balanceAfter)" }
    public let amount: Double
    public let balanceAfter: Double
    public let transactionType: String
    public let description: String
    public let createdAt: String
}
