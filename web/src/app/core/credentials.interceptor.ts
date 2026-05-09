import { HttpInterceptorFn } from '@angular/common/http';

const MUTATING_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);
const CSRF_COOKIE_NAME = 'XSRF-TOKEN';
const CSRF_HEADER_NAME = 'X-XSRF-TOKEN';

function readCookie(name: string): string | null {
  if (typeof document === 'undefined' || !document.cookie) {
    return null;
  }
  const match = document.cookie.match(new RegExp('(^|; )' + name + '=([^;]*)'));
  return match ? decodeURIComponent(match[2]) : null;
}

export const credentialsInterceptor: HttpInterceptorFn = (req, next) => {
  let modified = req.clone({ withCredentials: true });

  if (MUTATING_METHODS.has(req.method.toUpperCase())) {
    const token = readCookie(CSRF_COOKIE_NAME);
    if (token && !req.headers.has(CSRF_HEADER_NAME)) {
      modified = modified.clone({ setHeaders: { [CSRF_HEADER_NAME]: token } });
    }
  }

  return next(modified);
};
