import { environment } from '@env/environment';

/**
 * Whether a request goes to this app's API — `environment.apiBaseUrl`, which is absolute in
 * development ("https://localhost:7099") and a path on the app's own origin when deployed
 * ("/api"). Compares origin and path, so neither "/apix" nor "https://elsewhere.example/api"
 * passes. The interceptors that add credentials or the tenant use it to keep them off any
 * other host.
 */
export function isApiRequest(
  url: string,
  apiBaseUrl: string = environment.apiBaseUrl,
  origin: string = globalThis.location?.origin ?? 'http://localhost',
): boolean {
  const target = new URL(url, origin);
  const base = new URL(apiBaseUrl, origin);
  const basePath = base.pathname.replace(/\/+$/, '');
  return (
    target.origin === base.origin &&
    (basePath === '' || target.pathname === basePath || target.pathname.startsWith(`${basePath}/`))
  );
}
