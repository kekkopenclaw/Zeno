import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { DatePipe } from '@angular/common';
import { SignalRService } from './core/services/signalr.service';
import { ActivityLogService } from './core/services/activity-log.service';

interface NavSection {
  items: NavItem[];
}
interface NavItem {
  path: string;
  label: string;
  icon: string;
}

/**
 * AppComponent (Root)
 *
 * Handles the main shell, navigation, topbar, sidebar, and overlays for Mission Control.
 * - Injects SignalRService to track 'live' vs. 'polling' state sitewide
 * - Exposes agent activity feed globally
 * - Initializes SignalR connection on app startup
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, DatePipe],
  template: `
<div class="shell" [class.sidebar-collapsed]="!sidebarOpen()">

  <!-- ── Sidebar ──────────────────────────────────────── -->
  <aside class="sidebar" [class.open]="sidebarOpen()">
    <!-- Logo -->
    <div class="sidebar-logo">
      <span class="logo-icon">✦</span>
      <span class="logo-text">Mission Control</span>
    </div>

    <!-- Nav sections -->
    <nav class="sidebar-nav">
      @for (section of navSections; track $index) {
        <div class="nav-section">
          @for (item of section.items; track item.path) {
            <a [routerLink]="item.path"
               routerLinkActive="nav-active"
               [routerLinkActiveOptions]="{exact: true}"
               class="nav-link"
               (click)="closeSidebarOnMobile()">
              <span class="nav-icon">{{item.icon}}</span>
              <span class="nav-label">{{item.label}}</span>
            </a>
          }
        </div>
      }
    </nav>

    <!-- User -->
    <div class="sidebar-footer">
      <div class="user-avatar">K</div>
      <span class="user-name">Kekko</span>
    </div>
  </aside>

  <!-- Overlay for mobile -->
  @if (sidebarOpen()) {
    <div class="sidebar-overlay" (click)="sidebarOpen.set(false)"></div>
  }

  <!-- ── Main ─────────────────────────────────────────── -->
  <div class="main-area">

    <!-- Top bar -->
    <header class="topbar">
      <!-- Hamburger toggle -->
      <button class="topbar-icon-btn sidebar-toggle" (click)="sidebarOpen.update(v => !v)" title="Toggle sidebar" aria-label="Toggle sidebar">
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
          <line x1="3" y1="6" x2="21" y2="6"/>
          <line x1="3" y1="12" x2="21" y2="12"/>
          <line x1="3" y1="18" x2="21" y2="18"/>
        </svg>
      </button>
      <div class="topbar-search">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
        <span class="topbar-search-text">Search <kbd>⌘K</kbd></span>
      </div>
      <div class="topbar-right">
        <div class="topbar-live" [class.disconnected]="!signalr.isConnected()">
          <span class="live-dot" [class.offline]="!signalr.isConnected()"></span>
          <span>{{signalr.isConnected() ? 'Live' : 'Polling'}}</span>
        </div>
        <button class="btn btn-ghost" style="font-size:11px;padding:4px 10px"
                (click)="togglePause()" title="Pause/resume the live activity feed">
          {{paused() ? '▶ Resume' : '⏸ Pause'}}
        </button>
        <button class="topbar-icon-btn" title="Refresh activity feed" (click)="refreshFeed()">↻</button>
      </div>
    </header>

    <!-- Page content -->
    <main class="content">
      <router-outlet />
    </main>

    <!-- Activity feed -->
    <div class="activity-feed">
      <div class="activity-header">
        <span class="activity-dot"></span>
        <span class="activity-title">AGENT ACTIVITY</span>
        <span class="activity-count">{{feed().length}}</span>
      </div>
      <div class="activity-list">
        @if (feed().length === 0) {
          <div class="activity-empty">Waiting for agent activity...</div>
        }
        @for (log of feed(); track log.id) {
          <div class="activity-row">
            <span class="activity-time">{{log.timestamp | date:'HH:mm:ss'}}</span>
            <span class="activity-msg">{{log.message}}</span>
          </div>
        }
      </div>
    </div>
  </div>
</div>
  `,
  styles: [`
    .shell {
      display: flex;
      height: 100vh;
      overflow: hidden;
      background: var(--bg-base);
      position: relative;
    }

    /* ── Sidebar ── */
    .sidebar {
      width: 196px;
      flex-shrink: 0;
      background: var(--bg-surface);
      border-right: 1px solid var(--border);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      transition: width 0.2s ease, transform 0.2s ease;
    }

    /* Collapsed state: hide sidebar by pushing it off-screen */
    .shell.sidebar-collapsed .sidebar {
      width: 0;
      border-right: none;
    }

    .sidebar-logo {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 14px 12px;
      border-bottom: 1px solid var(--border);
      white-space: nowrap;
      overflow: hidden;
    }
    .logo-icon { color: var(--accent); font-size: 16px; flex-shrink: 0; }
    .logo-text { font-weight: 700; font-size: 13px; color: var(--text-primary); letter-spacing: 0.02em; }
    .sidebar-nav {
      flex: 1;
      overflow-y: auto;
      padding: 6px 6px;
    }
    .nav-section {
      padding-bottom: 6px;
      border-bottom: 1px solid var(--border);
      margin-bottom: 6px;
    }
    .nav-section:last-child { border-bottom: none; margin-bottom: 0; }
    .nav-link {
      display: flex;
      align-items: center;
      gap: 7px;
      padding: 5px 8px;
      border-radius: 5px;
      text-decoration: none;
      color: var(--text-secondary);
      font-size: 12.5px;
      transition: background 0.1s, color 0.1s;
      margin-bottom: 1px;
      white-space: nowrap;
    }
    .nav-link:hover { background: var(--bg-hover); color: var(--text-primary); }
    .nav-active { background: var(--bg-hover) !important; color: var(--text-primary) !important; }
    .nav-icon { font-size: 13px; width: 16px; text-align: center; flex-shrink: 0; }
    .nav-label { flex: 1; }
    .sidebar-footer {
      padding: 10px 12px;
      border-top: 1px solid var(--border);
      display: flex;
      align-items: center;
      gap: 8px;
      white-space: nowrap;
      overflow: hidden;
    }
    .user-avatar {
      width: 24px; height: 24px;
      background: var(--accent-dim);
      border: 1px solid var(--accent);
      border-radius: 50%;
      display: flex; align-items: center; justify-content: center;
      font-size: 10px; font-weight: 700; color: var(--accent);
      flex-shrink: 0;
    }
    .user-name { font-size: 12px; color: var(--text-secondary); }

    /* Overlay for mobile */
    .sidebar-overlay {
      display: none;
    }

    /* ── Main ── */
    .main-area {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
    }

    /* ── Top bar ── */
    .topbar {
      height: 44px;
      flex-shrink: 0;
      background: var(--bg-surface);
      border-bottom: 1px solid var(--border);
      display: flex;
      align-items: center;
      padding: 0 12px;
      gap: 8px;
    }
    .sidebar-toggle {
      flex-shrink: 0;
    }
    .topbar-search {
      display: flex;
      align-items: center;
      gap: 7px;
      background: var(--bg-elevated);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 5px 10px;
      color: var(--text-muted);
      cursor: text;
      width: 200px;
      flex-shrink: 0;
    }
    .topbar-search-text { font-size: 12px; flex: 1; display: flex; align-items: center; justify-content: space-between; }
    .topbar-search-text kbd {
      font-size: 10px;
      background: var(--bg-surface);
      border: 1px solid var(--border);
      border-radius: 3px;
      padding: 0 4px;
      color: var(--text-muted);
    }
    .topbar-right { margin-left: auto; display: flex; align-items: center; gap: 8px; }
    .topbar-live {
      display: flex; align-items: center; gap: 5px;
      font-size: 11px; color: var(--text-muted);
    }
    .topbar-live.disconnected { color: #f59e0b; }
    .live-dot {
      width: 6px; height: 6px;
      background: var(--green);
      border-radius: 50%;
      animation: pulse 2s infinite;
    }
    .live-dot.offline { background: #f59e0b; animation: none; }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.4; }
    }
    .topbar-icon-btn {
      background: none; border: none; cursor: pointer;
      color: var(--text-muted); font-size: 14px;
      width: 28px; height: 28px;
      display: flex; align-items: center; justify-content: center;
      border-radius: 5px;
    }
    .topbar-icon-btn:hover { background: var(--bg-hover); color: var(--text-primary); }

    /* ── Content ── */
    .content {
      flex: 1;
      overflow: auto;
      min-height: 0;
    }

    /* ── Activity feed ── */
    .activity-feed {
      flex-shrink: 0;
      height: 130px;
      background: var(--bg-surface);
      border-top: 1px solid var(--border);
      display: flex;
      flex-direction: column;
    }
    .activity-header {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 6px 14px;
      border-bottom: 1px solid var(--border);
    }
    .activity-dot {
      width: 5px; height: 5px;
      background: var(--accent);
      border-radius: 50%;
    }
    .activity-title {
      font-size: 10px; font-weight: 600; letter-spacing: 0.1em;
      color: var(--text-muted); text-transform: uppercase;
    }
    .activity-count {
      margin-left: auto;
      font-size: 10px;
      background: var(--bg-elevated);
      border: 1px solid var(--border);
      padding: 0 5px;
      border-radius: 3px;
      color: var(--text-muted);
    }
    .activity-list {
      flex: 1; overflow-y: auto; padding: 4px 14px;
    }
    .activity-empty { color: var(--text-muted); font-size: 12px; padding: 8px 0; }
    .activity-row {
      display: flex; align-items: flex-start; gap: 10px;
      padding: 3px 0;
      border-bottom: 1px solid rgba(34,34,38,0.6);
    }
    .activity-time {
      color: var(--text-muted); font-size: 10px;
      font-family: 'SF Mono', 'Fira Code', monospace;
      flex-shrink: 0; margin-top: 1px;
    }
    .activity-msg { font-size: 12px; color: var(--text-secondary); line-height: 1.4; }

    /* ── Responsive ── */
    @media (max-width: 768px) {
      .sidebar {
        position: fixed;
        top: 0;
        left: 0;
        height: 100vh;
        z-index: 100;
        transform: translateX(-100%);
        width: 220px !important;
      }
      .sidebar.open {
        transform: translateX(0);
        box-shadow: 4px 0 20px rgba(0,0,0,0.5);
      }
      .shell.sidebar-collapsed .sidebar {
        width: 220px !important;
      }
      .sidebar-overlay {
        display: block;
        position: fixed;
        inset: 0;
        background: rgba(0,0,0,0.5);
        z-index: 99;
      }
      .topbar-search {
        display: none;
      }
      .activity-feed {
        height: 90px;
      }
    }

    @media (max-width: 480px) {
      .topbar-right .btn {
        display: none;
      }
    }
  `],
})
export class App implements OnInit, OnDestroy {
  readonly signalr = inject(SignalRService);
  private logService = inject(ActivityLogService);

  feed = this.signalr.activityFeed;
  paused = signal(false);
  sidebarOpen = signal(true);

  navSections: NavSection[] = [
    {
      items: [
        { path: '/dashboard', label: 'Dashboard', icon: '⚡' },
        { path: '/tasks',     label: 'Tasks',     icon: '☰' },
        { path: '/agents',    label: 'Agents',    icon: '🤖' },
        { path: '/content',   label: 'Content',   icon: '📄' },
        { path: '/approvals', label: 'Approvals', icon: '✓' },
        { path: '/council',   label: 'Council',   icon: '⊕' },
        { path: '/calendar',  label: 'Calendar',  icon: '📅' },
      ],
    },
    {
      items: [
        { path: '/projects',  label: 'Projects',  icon: '📁' },
        { path: '/memory',    label: 'Memory',    icon: '💾' },
        { path: '/logs',      label: 'Logs',      icon: '📊' },
        { path: '/people',    label: 'People',    icon: '👥' },
        { path: '/teams',     label: 'Teams',     icon: '🤝' },
        { path: '/office',    label: 'Office',    icon: '🏢' },
        { path: '/team',      label: 'Team',      icon: '🌐' },
      ],
    },
    {
      items: [
        { path: '/system',    label: 'System',    icon: '⚙' },
        { path: '/radar',     label: 'Radar',     icon: '◎' },
        { path: '/factory',   label: 'Factory',   icon: '🏭' },
        { path: '/pipeline',  label: 'Pipeline',  icon: '→' },
        { path: '/ai-lab',    label: 'AI Lab',    icon: '🧪' },
        { path: '/feedback',  label: 'Feedback',  icon: '💬' },
      ],
    },
  ];

  ngOnInit(): void {
    this.signalr.startConnection();
    this.loadFeed();
    // Start with sidebar closed on mobile
    if (window.innerWidth < 768) {
      this.sidebarOpen.set(false);
    }
  }

  closeSidebarOnMobile(): void {
    if (window.innerWidth < 768) {
      this.sidebarOpen.set(false);
    }
  }

  togglePause(): void {
    this.paused.update(v => !v);
    if (this.paused()) {
      this.signalr.stopConnection();
    } else {
      this.signalr.startConnection();
      this.loadFeed();
    }
  }

  refreshFeed(): void {
    this.loadFeed();
  }

  private loadFeed(): void {
    this.logService.getByProject(1).subscribe(logs => {
      const sorted = [...logs].sort(
        (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime(),
      );
      this.signalr.activityFeed.set(sorted.slice(0, 20));
    });
  }

  ngOnDestroy(): void {
    this.signalr.stopConnection();
  }
}
