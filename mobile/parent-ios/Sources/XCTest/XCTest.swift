import Foundation

open class XCTestCase {
    public init() {}
    
    open func setUp() {
        // Default implementation
    }
    
    open func tearDown() {
        // Default implementation
    }
}

public func XCTAssertEqual<T: Equatable>(_ expression1: @autoclosure () throws -> T, _ expression2: @autoclosure () throws -> T, _ message: @autoclosure () -> String = "", file: StaticString = #file, line: UInt = #line) {
    do {
        let v1 = try expression1()
        let v2 = try expression2()
        if v1 != v2 {
            print("❌ Assertion Failed: \(v1) is not equal to \(v2). \(message()) at \(file):\(line)")
            exit(1)
        }
    } catch {
        print("❌ Assertion Failed with error: \(error). \(message()) at \(file):\(line)")
        exit(1)
    }
}

public func XCTAssertNotEqual<T: Equatable>(_ expression1: @autoclosure () throws -> T, _ expression2: @autoclosure () throws -> T, _ message: @autoclosure () -> String = "", file: StaticString = #file, line: UInt = #line) {
    do {
        let v1 = try expression1()
        let v2 = try expression2()
        if v1 == v2 {
            print("❌ Assertion Failed: \(v1) is equal to \(v2). \(message()) at \(file):\(line)")
            exit(1)
        }
    } catch {
        print("❌ Assertion Failed with error: \(error). \(message()) at \(file):\(line)")
        exit(1)
    }
}

public func XCTAssertNotNil(_ expression: @autoclosure () throws -> Any?, _ message: @autoclosure () -> String = "", file: StaticString = #file, line: UInt = #line) {
    do {
        let v = try expression()
        if v == nil {
            print("❌ Assertion Failed: value is nil. \(message()) at \(file):\(line)")
            exit(1)
        }
    } catch {
        print("❌ Assertion Failed with error: \(error). \(message()) at \(file):\(line)")
        exit(1)
    }
}

public func XCTAssertNil(_ expression: @autoclosure () throws -> Any?, _ message: @autoclosure () -> String = "", file: StaticString = #file, line: UInt = #line) {
    do {
        let v = try expression()
        if v != nil {
            print("❌ Assertion Failed: \(String(describing: v)) is not nil. \(message()) at \(file):\(line)")
            exit(1)
        }
    } catch {
        print("❌ Assertion Failed with error: \(error). \(message()) at \(file):\(line)")
        exit(1)
    }
}

public func XCTAssertTrue(_ expression: @autoclosure () throws -> Bool, _ message: @autoclosure () -> String = "", file: StaticString = #file, line: UInt = #line) {
    do {
        let v = try expression()
        if !v {
            print("❌ Assertion Failed: expected true. \(message()) at \(file):\(line)")
            exit(1)
        }
    } catch {
        print("❌ Assertion Failed with error: \(error). \(message()) at \(file):\(line)")
        exit(1)
    }
}

public func XCTAssertFalse(_ expression: @autoclosure () throws -> Bool, _ message: @autoclosure () -> String = "", file: StaticString = #file, line: UInt = #line) {
    do {
        let v = try expression()
        if v {
            print("❌ Assertion Failed: expected false. \(message()) at \(file):\(line)")
            exit(1)
        }
    } catch {
        print("❌ Assertion Failed with error: \(error). \(message()) at \(file):\(line)")
        exit(1)
    }
}
