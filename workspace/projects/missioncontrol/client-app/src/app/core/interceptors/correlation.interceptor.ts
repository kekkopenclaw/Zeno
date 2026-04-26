import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { FrontendLogService } from '../services/frontend-log.service';

let sessionCorrelationId: string | null = null;

function getOrCreateSessionCorrelationId(): string {
  if (!sessionCorrelationId) {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      sessionCorrelationId = crypto.randomUUID().replace(/-/g, '');
    } else {
      sessionCorrelationId = Math.random().toString(36).slice(2) + Date.now().toString(36);
    }
  }
  return sessionCorrelationId;
}

export const correlationInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
) => {
  const logService = inject(FrontendLogService);
  const correlationId = getOrCreateSessionCorrelationId();

  const modified = req.clone({
    setHeaders: { 'X-Correlation-Id': correlationId },
  });

  return next(modified).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        logService.error(
          `HTTP ${err.status} on ${req.method} ${req.urlWithParams}: ${err.message}`,
          undefined,
          req.urlWithParams,
        );
      }
      return throwError(() => err);
    }),
  );
};
