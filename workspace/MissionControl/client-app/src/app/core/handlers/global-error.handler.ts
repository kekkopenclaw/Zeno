import { ErrorHandler, Injectable, inject, NgZone } from '@angular/core';
import { FrontendLogService } from '../services/frontend-log.service';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private logService = inject(FrontendLogService);
  private zone = inject(NgZone);

  handleError(error: unknown): void {
    const message =
      error instanceof Error
        ? error.message
        : typeof error === 'string'
          ? error
          : 'Unknown error';

    const stackTrace = error instanceof Error ? error.stack : undefined;

    console.error('[GlobalErrorHandler]', error);

    this.zone.runOutsideAngular(() => {
      this.logService.error(message, stackTrace, window.location.href);
    });
  }
}
