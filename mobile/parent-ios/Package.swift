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
            path: "Sources/NaderGorgeParent",
            exclude: ["NaderGorgeParentApp.swift"],
            resources: [.process("Resources")]),
        .testTarget(
            name: "NaderGorgeParentTests",
            dependencies: ["NaderGorgeParent"],
            path: "Tests/NaderGorgeParentTests"),
    ]
)
