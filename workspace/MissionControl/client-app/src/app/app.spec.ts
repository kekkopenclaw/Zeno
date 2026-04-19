import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { SignalRService } from './core/services/signalr.service';
import { ActivityLogService } from './core/services/activity-log.service';

describe('App', () => {
  beforeEach(async () => {
    const signalRMock = {
      activityFeed: signal([]),
      isConnected: signal(false),
      startConnection: () => {},
      stopConnection: () => {},
    };
    const activityMock = {
      getByProject: () => ({ subscribe: (fn: (logs: []) => void) => fn([]) }),
    };

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        { provide: SignalRService, useValue: signalRMock },
        { provide: ActivityLogService, useValue: activityMock },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render shell title', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.logo-text')?.textContent).toContain('Mission Control');
  });
});
