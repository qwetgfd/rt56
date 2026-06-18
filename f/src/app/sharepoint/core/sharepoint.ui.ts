import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SP_UI } from './sharepoint.messages';

export type SpIconName =
  | 'home'
  | 'back'
  | 'apps'
  | 'workspace'
  | 'folder'
  | 'folder-open'
  | 'file'
  | 'plus'
  | 'edit'
  | 'trash'
  | 'play'
  | 'search'
  | 'grid'
  | 'list'
  | 'check'
  | 'alert'
  | 'info'
  | 'eye'
  | 'eye-off'
  | 'refresh'
  | 'chevron-left'
  | 'chevron-right'
  | 'unlink'
  | 'download'
  | 'code'
  | 'settings'
  | 'cloud'
  | 'shield'
  | 'building'
  | 'copy'
  | 'x';

@Component({
  selector: 'sp-icon',
  standalone: true,
  imports: [CommonModule],
  host: {
    '[attr.title]': 'title',
  },
  template: `
    <svg
      class="sp-icon"
      [class.sp-icon--sm]="size === 'sm'"
      [class.sp-icon--lg]="size === 'lg'"
      [class.sp-icon--spin]="spin"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false">
      @switch (name) {
        @case ('home') {
          <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
          <path d="M9 22V12h6v10" />
        }
        @case ('back') {
          <path d="m15 18-6-6 6-6" />
        }
        @case ('chevron-left') {
          <path d="m15 18-6-6 6-6" />
        }
        @case ('play') {
          <path d="m9 18 6-6-6-6" />
        }
        @case ('chevron-right') {
          <path d="m9 18 6-6-6-6" />
        }
        @case ('apps') {
          <g>
            <rect x="3" y="3" width="7" height="7" rx="1.5" />
            <rect x="14" y="3" width="7" height="7" rx="1.5" />
            <rect x="14" y="14" width="7" height="7" rx="1.5" />
            <rect x="3" y="14" width="7" height="7" rx="1.5" />
          </g>
        }
        @case ('grid') {
          <g>
            <rect x="3" y="3" width="7" height="7" rx="1.5" />
            <rect x="14" y="3" width="7" height="7" rx="1.5" />
            <rect x="14" y="14" width="7" height="7" rx="1.5" />
            <rect x="3" y="14" width="7" height="7" rx="1.5" />
          </g>
        }
        @case ('workspace') {
          <path d="M3 7h5l2 3h11v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z" />
          <path d="M3 7V5a2 2 0 0 1 2-2h4l2 4" />
        }
        @case ('folder') {
          <path d="M3 7h5l2 3h11v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z" />
        }
        @case ('folder-open') {
          <path d="M6 14h12" />
          <path d="M6 18h12" />
          <path d="M6 10h12" />
          <path d="M8 6h8l2 4H6l2-4Z" />
        }
        @case ('file') {
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <path d="M14 2v6h6" />
        }
        @case ('plus') {
          <path d="M12 5v14" />
          <path d="M5 12h14" />
        }
        @case ('edit') {
          <path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z" />
        }
        @case ('trash') {
          <path d="M3 6h18" />
          <path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6" />
          <path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2" />
        }
        @case ('search') {
          <circle cx="11" cy="11" r="8" />
          <path d="m21 21-4.3-4.3" />
        }
        @case ('list') {
          <path d="M8 6h13" />
          <path d="M8 12h13" />
          <path d="M8 18h13" />
          <path d="M3 6h.01" />
          <path d="M3 12h.01" />
          <path d="M3 18h.01" />
        }
        @case ('check') {
          <path d="M20 6 9 17l-5-5" />
        }
        @case ('alert') {
          <path d="m21.7 18-8.5-14.6a1.4 1.4 0 0 0-2.4 0L2.3 18a1.4 1.4 0 0 0 1.2 2h17a1.4 1.4 0 0 0 1.2-2Z" />
          <path d="M12 9v4" />
          <path d="M12 17h.01" />
        }
        @case ('info') {
          <circle cx="12" cy="12" r="10" />
          <path d="M12 16v-4" />
          <path d="M12 8h.01" />
        }
        @case ('eye') {
          <g>
            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
            <circle cx="12" cy="12" r="3" />
          </g>
        }
        @case ('eye-off') {
          <g>
            <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
            <path d="M1 1l22 22" />
          </g>
        }
        @case ('refresh') {
          <path d="M21 2v6h-6" />
          <path d="M3 12a9 9 0 0 1 15-6.7L21 8" />
          <path d="M3 22v-6h6" />
          <path d="M21 12a9 9 0 0 1-15 6.7L3 16" />
        }
        @case ('unlink') {
          <path d="M10 13a5 5 0 0 0 7.1 0l2-2a5 5 0 0 0-7.1-7.1l-1.1 1.1" />
          <path d="M14 11a5 5 0 0 0-7.1 0l-2 2A5 5 0 0 0 12 20.1l1.1-1.1" />
          <path d="M3 3l18 18" />
        }
        @case ('download') {
          <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
          <path d="m7 10 5 5 5-5" />
          <path d="M12 15V3" />
        }
        @case ('code') {
          <path d="m16 18 6-6-6-6" />
          <path d="m8 6-6 6 6 6" />
        }
        @case ('settings') {
          <path d="M12 15.5A3.5 3.5 0 1 0 12 8.5a3.5 3.5 0 0 0 0 7Z" />
          <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1Z" />
        }
        @case ('cloud') {
          <path d="M17.5 19H9a7 7 0 1 1 4.7-12.3A5.5 5.5 0 0 1 17.5 19Z" />
        }
        @case ('shield') {
          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z" />
        }
        @case ('building') {
          <path d="M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18Z" />
          <path d="M6 12H4a2 2 0 0 0-2 2v8h20v-8a2 2 0 0 0-2-2h-2" />
          <path d="M10 6h4" />
          <path d="M10 10h4" />
          <path d="M10 14h4" />
          <path d="M10 18h4" />
        }
        @case ('copy') {
          <rect x="8" y="8" width="14" height="14" rx="2" />
          <path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2" />
        }
        @case ('x') {
          <path d="M18 6 6 18" />
          <path d="m6 6 12 12" />
        }
      }
    </svg>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        line-height: 0;
        flex-shrink: 0;
        color: inherit;
      }
      .sp-icon {
        width: 18px;
        height: 18px;
      }
      .sp-icon--sm {
        width: 16px;
        height: 16px;
      }
      .sp-icon--lg {
        width: 28px;
        height: 28px;
      }
    `,
  ],
})
export class SharepointIconComponent {
  @Input({ required: true }) name!: SpIconName;
  @Input() size: 'sm' | 'md' | 'lg' = 'md';
  @Input() spin = false;
  @Input() title?: string;
}

export type SpStatusAlertsMode = 'alert' | 'toast' | 'banner';

@Component({
  selector: 'sp-status-alerts',
  standalone: true,
  imports: [CommonModule, SharepointIconComponent],
  styleUrls: ['../sharepoint.styles.css'],
  styles: [':host { display: contents; }'],
  template: `
    @switch (mode) {
      @case ('toast') {
        @if (error || success) {
          <div class="sp-toast-host" aria-live="polite">
            @if (error) {
              <div class="sp-toast sp-toast--error" role="alert" aria-live="assertive">
                <sp-icon class="sp-toast__icon" name="alert" size="sm" />
                <p class="sp-toast__message">{{ error }}</p>
                @if (dismissibleError) {
                  <button type="button" class="sp-toast__dismiss" (click)="dismissError.emit()" [attr.aria-label]="m.dismissError">
                    <sp-icon name="x" size="sm" />
                  </button>
                }
              </div>
            }
            @if (success) {
              <div class="sp-toast sp-toast--success" role="status">
                <sp-icon class="sp-toast__icon" name="check" size="sm" />
                <p class="sp-toast__message">{{ success }}</p>
                <button type="button" class="sp-toast__dismiss" (click)="dismissSuccess.emit()" [attr.aria-label]="m.dismissNotification">
                  <sp-icon name="x" size="sm" />
                </button>
              </div>
            }
          </div>
        }
      }
      @case ('alert') {
        @if (error) {
          <div class="sp-alert sp-alert--error" role="alert" aria-live="assertive">
            <sp-icon class="sp-alert__icon" name="alert" />
            <div class="sp-alert__body">
              <strong>{{ m.errorHeading }}</strong>
              <p>{{ error }}</p>
            </div>
          </div>
        }
        @if (success) {
          <div class="sp-alert sp-alert--success" role="status" aria-live="polite">
            <sp-icon class="sp-alert__icon" name="check" />
            <div class="sp-alert__body">
              <strong>{{ m.successHeading }}</strong>
              <p>{{ success }}</p>
            </div>
            <button type="button" class="sp-alert__dismiss" (click)="dismissSuccess.emit()" [attr.aria-label]="m.dismissSuccess">
              <sp-icon name="x" size="sm" />
            </button>
          </div>
        }
      }
      @case ('banner') {
        @if (error) {
          <div class="sp-reg-banner sp-reg-banner--error" role="alert" aria-live="assertive">{{ error }}</div>
        }
        @if (success) {
          <div class="sp-reg-banner sp-reg-banner--success" role="status" aria-live="polite">
            <span>{{ success }}</span>
            <button type="button" class="sp-reg-banner__close" (click)="dismissSuccess.emit()" [attr.aria-label]="m.dismiss">×</button>
          </div>
        }
      }
    }
  `,
})
export class SharepointStatusAlertsComponent {
  readonly m = SP_UI.status;

  @Input() error = '';
  @Input() success = '';
  @Input() mode: SpStatusAlertsMode = 'alert';
  @Input() dismissibleError = false;
  @Output() dismissError = new EventEmitter<void>();
  @Output() dismissSuccess = new EventEmitter<void>();
}

@Component({
  selector: 'sp-sharepoint-logo',
  standalone: true,
  template: `
    <img
      class="sp-sharepoint-logo"
      src="/sharepoint/sharepoint-mark.svg"
      alt=""
      aria-hidden="true"
      focusable="false" />
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        line-height: 0;
      }
      .sp-sharepoint-logo {
        width: var(--sp-sharepoint-logo-size, 28px);
        height: var(--sp-sharepoint-logo-size, 28px);
        object-fit: contain;
        display: block;
      }
    `,
  ],
})
export class SharepointLogoComponent {}

@Component({
  selector: 'sp-tp-logo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="sp-tp-logo"
      [class.sp-tp-logo--pa]="resolvedFile === 'pa'"
      [class.sp-tp-logo--pa-big]="resolvedFile === 'pa-big'"
      [class.sp-tp-logo--pa-big-d]="resolvedFile === 'pa-big-d'"
      [attr.aria-hidden]="decorative ? 'true' : null"
      [attr.role]="decorative ? null : 'img'"
      [attr.aria-label]="decorative ? null : 'TEP'">
      <img class="sp-tp-logo__img" [src]="logoSrc" alt="" />
    </span>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        line-height: 0;
      }
      .sp-tp-logo {
        display: inline-flex;
        align-items: center;
        height: var(--sp-tp-logo-size, 28px);
      }
      .sp-tp-logo__img {
        height: var(--sp-tp-logo-size, 28px);
        width: auto;
        display: block;
        object-fit: contain;
        object-position: left center;
      }
      .sp-tp-logo--pa .sp-tp-logo__img {
        aspect-ratio: 1552 / 1645;
      }
      .sp-tp-logo--pa-big .sp-tp-logo__img,
      .sp-tp-logo--pa-big-d .sp-tp-logo__img {
        aspect-ratio: 1567 / 275;
        max-width: min(100%, calc(var(--sp-tp-logo-size, 28px) * 5.7));
      }
    `,
  ],
})
export class SharepointTpLogoComponent {
  /** Direct mapping to one of the three brand SVG assets. */
  @Input() file?: 'pa' | 'pa-big' | 'pa-big-d';
  @Input() decorative = true;

  get resolvedFile(): 'pa' | 'pa-big' | 'pa-big-d' {
    return this.file ?? 'pa';
  }

  get logoSrc(): string {
    switch (this.resolvedFile) {
      case 'pa-big':
        return '/sharepoint/tep-pa-big.svg';
      case 'pa-big-d':
        return '/sharepoint/tep-pa-big-d.svg';
      default:
        return '/sharepoint/tep-pa.svg';
    }
  }
}

type SpOrbitLoaderStep = { readonly index: number; readonly label: string };

@Component({
  selector: 'sp-orbit-loader',
  standalone: true,
  imports: [CommonModule, SharepointIconComponent, SharepointLogoComponent],
  template: `
    <div
      class="sp-orbit-loader"
      [class.sp-orbit-loader--compact]="compact"
      role="status"
      [attr.aria-label]="ariaLabel || title || 'Loading'">
      <div class="sp-orbit-loader__visual" aria-hidden="true">
        <span class="sp-orbit-loader__glow"></span>
        <span class="sp-orbit-loader__ring sp-orbit-loader__ring--outer"></span>
        <span class="sp-orbit-loader__ring sp-orbit-loader__ring--inner"></span>
        <div class="sp-orbit-loader__track">
          <span class="sp-orbit-loader__sat sp-orbit-loader__sat--a">
            <span class="sp-orbit-loader__sat-inner"><sp-icon name="folder" size="sm" /></span>
          </span>
          <span class="sp-orbit-loader__sat sp-orbit-loader__sat--b">
            <span class="sp-orbit-loader__sat-inner"><sp-icon name="cloud" size="sm" /></span>
          </span>
          <span class="sp-orbit-loader__sat sp-orbit-loader__sat--c">
            <span class="sp-orbit-loader__sat-inner"><sp-icon name="file" size="sm" /></span>
          </span>
        </div>
        <span class="sp-orbit-loader__core">
          <sp-sharepoint-logo />
        </span>
      </div>
      @if (title) {
        <p class="sp-orbit-loader__title">{{ title }}</p>
      }
      @if (hint) {
        <p class="sp-orbit-loader__hint">{{ hint }}</p>
      }
      <div class="sp-orbit-loader__bar" aria-hidden="true"></div>
      @if (showSteps && steps.length) {
        <div class="sp-orbit-loader__steps" aria-hidden="true">
          @for (step of steps; track step.index; let last = $last) {
            <span class="sp-orbit-loader__step" [class.sp-orbit-loader__step--active]="activeStep >= step.index">{{ step.label }}</span>
            @if (!last) {
              <span class="sp-orbit-loader__step-sep"></span>
            }
          }
        </div>
      }
    </div>
  `,
  styleUrls: ['./sharepoint-orbit-loader.css'],
  styles: [`:host { display: block; width: 100%; }`],
})
export class SharepointOrbitLoaderComponent {
  @Input() title = '';
  @Input() hint = '';
  @Input() ariaLabel = '';
  @Input() compact = false;
  @Input() showSteps = false;
  @Input() activeStep = 1;
  @Input() steps: readonly SpOrbitLoaderStep[] = [];
}
