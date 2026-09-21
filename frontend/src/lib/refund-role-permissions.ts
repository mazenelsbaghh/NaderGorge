export function permissionsForRefundPage(
  permissions: string[],
  allowedDomain: string,
  allowedNavbarItems: string[],
): string[] {
  if (allowedDomain !== 'assistant' || !allowedNavbarItems.includes('/assistant/refunds')) {
    return permissions;
  }
  return [...new Set([...permissions, 'finance.refunds.view', 'finance.refunds.create'])];
}
