const DEFAULT_API_URL = 'http://localhost:5245/api';

export function getBackendOrigin(): string {
  const configuredBackendUrl = process.env.NEXT_PUBLIC_BACKEND_URL;
  if (configuredBackendUrl) {
    return configuredBackendUrl.replace(/\/$/, '');
  }

  const apiUrl = process.env.NEXT_PUBLIC_API_URL || DEFAULT_API_URL;
  return apiUrl.replace(/\/api\/?$/, '').replace(/\/$/, '');
}

export function getBackendHubUrl(hubPath: string): string {
  const normalizedPath = hubPath.startsWith('/') ? hubPath : `/${hubPath}`;
  const configuredHubUrl = process.env.NEXT_PUBLIC_WS_URL;
  if (configuredHubUrl) return `${configuredHubUrl.replace(/\/$/, '')}${normalizedPath}`;

  const backendOrigin = getBackendOrigin();
  try {
    const url = new URL(backendOrigin);
    if (url.hostname === 'api.massar-academy.net') url.hostname = 'ws.massar-academy.net';
    return `${url.toString().replace(/\/$/, '')}${normalizedPath}`;
  } catch {
    return `${backendOrigin}${normalizedPath}`;
  }
}
