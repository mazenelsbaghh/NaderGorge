import SwiftUI

@MainActor
public struct LinkingView: View {
    @StateObject private var viewModel: LinkingViewModel
    public var onLinkSuccess: () -> Void
    
    public init(viewModel: LinkingViewModel? = nil, onLinkSuccess: @escaping () -> Void) {
        self._viewModel = StateObject(wrappedValue: viewModel ?? LinkingViewModel())
        self.onLinkSuccess = onLinkSuccess
    }
    
    public var body: some View {
        ZStack {
            LiquidBackgroundView()
                .ignoresSafeArea()
            
            VStack(spacing: 30) {
                Spacer()
                
                VStack(spacing: 12) {
                    Image(systemName: "person.2.crop.square.stack.fill")
                        .font(.system(size: 60))
                        .foregroundStyle(
                            LinearGradient(
                                colors: [.cyan, .purple],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .shadow(color: .cyan.opacity(0.3), radius: 10)
                    
                    Text("بوابة متابعة أولياء الأمور")
                        .font(.title)
                        .fontWeight(.bold)
                        .foregroundColor(.white)
                    
                    Text("نظام المنصة التعليمية للأستاذ نادر جورج")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.6))
                }
                
                VStack(spacing: 20) {
                    Text("ربط حساب الطالب")
                        .font(.headline)
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity, alignment: .trailing)
                    
                    Text("أدخل رمز المتابعة المكون من 6 أحرف والموجود في حساب طالبك.")
                        .font(.footnote)
                        .foregroundColor(.white.opacity(0.7))
                        .multilineTextAlignment(.trailing)
                        .frame(maxWidth: .infinity, alignment: .trailing)
                    
                    let inputField = TextField("", text: $viewModel.trackingCode, prompt: Text("مثال: NG79F4").foregroundColor(.white.opacity(0.3)))
                        .font(.system(size: 24, weight: .bold, design: .monospaced))
                        .multilineTextAlignment(.center)
                        .foregroundColor(.white)
                        .padding()
                        .background(Color.white.opacity(0.08))
                        .cornerRadius(12)
                        .overlay(
                            RoundedRectangle(cornerRadius: 12)
                                .stroke(Color.white.opacity(0.2), lineWidth: 1)
                        )
                        .autocorrectionDisabled()
                        .onChange(of: viewModel.trackingCode) { oldValue, newValue in
                            if newValue.count > 6 {
                                viewModel.trackingCode = String(newValue.prefix(6))
                            }
                        }
                    
                    #if os(iOS)
                    inputField
                        .textInputAutocapitalization(.characters)
                    #else
                    inputField
                    #endif
                    
                    if let error = viewModel.errorMessage {
                        Text(error)
                            .font(.caption)
                            .foregroundColor(.red)
                            .multilineTextAlignment(.trailing)
                            .frame(maxWidth: .infinity, alignment: .trailing)
                            .transition(.opacity)
                    }
                    
                    if let success = viewModel.successMessage {
                        Text(success)
                            .font(.caption)
                            .foregroundColor(.green)
                            .multilineTextAlignment(.trailing)
                            .frame(maxWidth: .infinity, alignment: .trailing)
                            .transition(.opacity)
                    }
                    
                    Button(action: {
                        Task {
                            await viewModel.linkStudent()
                            if viewModel.successMessage != nil {
                                try? await Task.sleep(nanoseconds: 1_500_000_000)
                                onLinkSuccess()
                            }
                        }
                    }) {
                        HStack {
                            if viewModel.isLoading {
                                ProgressView()
                                    .tint(.black)
                                    .padding(.trailing, 8)
                            }
                            Text("ربط الحساب")
                                .fontWeight(.bold)
                        }
                        .foregroundColor(.black)
                        .frame(maxWidth: .infinity)
                        .padding()
                        .background(
                            LinearGradient(
                                colors: [Color.cyan, Color.blue],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .cornerRadius(12)
                        .shadow(color: .cyan.opacity(0.4), radius: 8, x: 0, y: 4)
                    }
                    .disabled(viewModel.isLoading || viewModel.trackingCode.count != 6)
                    .opacity(viewModel.trackingCode.count == 6 ? 1.0 : 0.6)
                    .animation(.spring(response: 0.4, dampingFraction: 0.8), value: viewModel.trackingCode)
                }
                .glassCard()
                .padding(.horizontal)
                
                Spacer()
            }
            .padding()
        }
    }
}
