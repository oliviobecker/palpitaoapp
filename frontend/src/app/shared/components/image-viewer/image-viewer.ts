import {
  Component,
  ChangeDetectionStrategy,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ImageViewerService } from '../../../core/notifications/image-viewer.service';
import { Icon } from '../icon/icon';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-image-viewer',
  imports: [TranslatePipe, Icon],
  template: `
    @if (viewer.state(); as state) {
      @if (state.open) {
        <div
          class="modal d-block"
          tabindex="-1"
          role="dialog"
          aria-modal="true"
          aria-labelledby="image-viewer-title"
          style="background: rgba(0,0,0,.85)"
          (keydown)="onKeydown($event)"
        >
          <div #dialog class="modal-dialog modal-dialog-centered modal-xl">
            <div class="modal-content">
              <div class="modal-header">
                <div class="min-w-0">
                  <h2 id="image-viewer-title" class="modal-title h6 mb-0 text-truncate">
                    {{ state.title }}
                  </h2>
                  @if (state.subtitle) {
                    <small class="text-body-secondary">{{ state.subtitle }}</small>
                  }
                </div>
                <button
                  #closeBtn
                  type="button"
                  class="btn btn-sm btn-outline-secondary"
                  [attr.aria-label]="'ocrHistory.close' | translate"
                  (click)="viewer.close()"
                >
                  <app-icon name="x" [size]="18" />
                </button>
              </div>
              <div class="modal-body text-center">
                <button
                  type="button"
                  class="viewer-zoom-btn"
                  [attr.aria-label]="
                    (zoomed() ? 'ocrHistory.zoomOut' : 'ocrHistory.zoomIn') | translate
                  "
                  (click)="toggleZoom()"
                >
                  <img
                    class="viewer-image"
                    [class.viewer-image--zoomed]="zoomed()"
                    [src]="state.src"
                    [alt]="state.title"
                  />
                </button>
              </div>
              <div class="modal-footer">
                <a
                  class="btn btn-outline-secondary"
                  [href]="state.src"
                  [download]="state.downloadName"
                >
                  <app-icon name="download" [size]="16" />
                  <span class="ms-1">{{ 'ocrHistory.download' | translate }}</span>
                </a>
                <button type="button" class="btn btn-primary" (click)="viewer.close()">
                  {{ 'ocrHistory.close' | translate }}
                </button>
              </div>
            </div>
          </div>
        </div>
      }
    }
  `,
  styles: [
    `
      .viewer-zoom-btn {
        display: block;
        width: 100%;
        padding: 0;
        border: 0;
        background: transparent;
      }
      .viewer-image {
        max-width: 100%;
        max-height: 70vh;
        object-fit: contain;
        border-radius: 12px;
        cursor: zoom-in;
      }
      /* Zoomed: drop the viewport cap and let the modal body scroll instead. */
      .viewer-image--zoomed {
        max-height: none;
        cursor: zoom-out;
      }
      .modal-body {
        overflow: auto;
      }
      .min-w-0 {
        min-width: 0;
      }
    `,
  ],
})
export class ImageViewer {
  protected readonly viewer = inject(ImageViewerService);

  private readonly dialog = viewChild<ElementRef<HTMLElement>>('dialog');
  private readonly closeBtn = viewChild<ElementRef<HTMLButtonElement>>('closeBtn');

  protected readonly zoomed = signal(false);

  /** Element focused before the viewer opened, restored when it closes. */
  private lastFocused: HTMLElement | null = null;

  constructor() {
    // Move focus into the dialog when it opens and restore it when it closes.
    effect(() => {
      const state = this.viewer.state();
      const btn = this.closeBtn();
      if (state.open) {
        if (!this.lastFocused) {
          this.lastFocused = document.activeElement as HTMLElement | null;
          this.zoomed.set(false);
        }
        btn?.nativeElement.focus();
      } else if (this.lastFocused) {
        this.lastFocused.focus();
        this.lastFocused = null;
      }
    });
  }

  protected toggleZoom(): void {
    this.zoomed.set(!this.zoomed());
  }

  /** ESC closes the viewer; Tab is trapped within its focusable elements. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.viewer.close();
      return;
    }
    if (event.key !== 'Tab') {
      return;
    }
    // Includes the download anchor, which a button-only selector would skip.
    const focusables = Array.from(
      this.dialog()?.nativeElement.querySelectorAll<HTMLElement>('button, a[href]') ?? [],
    ).filter((el) => !(el as HTMLButtonElement).disabled);
    if (focusables.length === 0) {
      return;
    }
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
