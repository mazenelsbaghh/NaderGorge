export function usesNativeProviderControls(provider: string): boolean {
  return provider.toLowerCase() === 'bunny';
}
