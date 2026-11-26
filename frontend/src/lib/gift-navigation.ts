export function giftManagementPath(pathname: string) {
  return pathname === '/assistant/gifts' || pathname.startsWith('/assistant/gifts/')
    ? '/assistant/gifts'
    : '/admin/gifts';
}
