import SwiftUI

@MainActor
public struct ParentAppContainer: View {
    @State private var hasLinkedStudent: Bool = false
    @StateObject private var dashboardViewModel = DashboardViewModel()
    
    public init() {}
    
    public var body: some View {
        Group {
            if hasLinkedStudent {
                DashboardView(viewModel: dashboardViewModel, onAddStudent: {
                    withAnimation(.spring()) {
                        hasLinkedStudent = false
                    }
                })
            } else {
                LinkingView(onLinkSuccess: {
                    dashboardViewModel.loadProfiles()
                    withAnimation(.spring()) {
                        hasLinkedStudent = !dashboardViewModel.linkedProfiles.isEmpty
                    }
                })
            }
        }
        .onAppear {
            dashboardViewModel.loadProfiles()
            hasLinkedStudent = !dashboardViewModel.linkedProfiles.isEmpty
        }
    }
}
