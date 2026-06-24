import Foundation
import Security

public class KeychainService {
    public static var shared = KeychainService()
    private let account = "NaderGorgeParentProfiles"
    private let service = "com.nadergorge.parent"
    
    // Fallback store for CLI test environments or missing entitlements
    private var fallbackStore: [StudentProfile] = []
    private var useFallback: Bool = false
    
    internal init(useFallback: Bool = false) {
        self.useFallback = useFallback
    }
    
    public func saveProfiles(_ profiles: [StudentProfile]) throws {
        if useFallback {
            fallbackStore = profiles
            return
        }
        
        let data = try JSONEncoder().encode(profiles)
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
        
        let attributes: [String: Any] = [
            kSecValueData as String: data
        ]
        
        let status = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        
        if status == errSecItemNotFound {
            var addQuery = query
            addQuery[kSecValueData as String] = data
            let addStatus = SecItemAdd(addQuery as CFDictionary, nil)
            if addStatus == -34018 { // errSecMissingEntitlement
                useFallback = true
                fallbackStore = profiles
                return
            }
            if addStatus != errSecSuccess {
                throw KeychainError.secError(addStatus)
            }
        } else if status == -34018 {
            useFallback = true
            fallbackStore = profiles
        } else if status != errSecSuccess {
            throw KeychainError.secError(status)
        }
    }
    
    public func loadProfiles() -> [StudentProfile] {
        if useFallback {
            return fallbackStore
        }
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]
        
        var dataTypeRef: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &dataTypeRef)
        
        if status == -34018 {
            useFallback = true
            return fallbackStore
        }
        
        guard status == errSecSuccess, let data = dataTypeRef as? Data else {
            return []
        }
        
        do {
            return try JSONDecoder().decode([StudentProfile].self, from: data)
        } catch {
            print("Failed to decode profiles from Keychain: \(error)")
            return []
        }
    }
    
    public func addProfile(_ profile: StudentProfile) throws {
        var profiles = loadProfiles()
        if let index = profiles.firstIndex(where: { $0.studentId == profile.studentId }) {
            profiles[index] = profile
        } else {
            profiles.append(profile)
        }
        try saveProfiles(profiles)
    }
    
    public func removeProfile(studentId: UUID) throws {
        var profiles = loadProfiles()
        profiles.removeAll { $0.studentId == studentId }
        try saveProfiles(profiles)
    }
    
    public func clear() throws {
        if useFallback {
            fallbackStore.removeAll()
            return
        }
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
        let status = SecItemDelete(query as CFDictionary)
        if status == -34018 {
            useFallback = true
            fallbackStore.removeAll()
            return
        }
        if status != errSecSuccess && status != errSecItemNotFound {
            throw KeychainError.secError(status)
        }
    }
}

public enum KeychainError: Error {
    case secError(OSStatus)
}
