// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "NaderGorgeParent",
    platforms: [
        .iOS(.v17),
        .macOS(.v14)
    ],
    products: [
        .library(
            name: "NaderGorgeParent",
            targets: ["NaderGorgeParent"]),
    ],
    dependencies: [],
    targets: [
        .target(
            name: "NaderGorgeParent",
            dependencies: [],
            path: "Sources/NaderGorgeParent"),
        .target(
            name: "XCTest",
            dependencies: [],
            path: "Sources/XCTest"),
        .testTarget(
            name: "NaderGorgeParentTests",
            dependencies: ["NaderGorgeParent", "XCTest"],
            path: "Tests/NaderGorgeParentTests"),
    ]
)
