import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from '../../core/notifications/toast.service';
import { copyToClipboard } from '../../shared/utils/clipboard.util';
import { Icon } from '../../shared/components/icon/icon';

/**
 * Presentational copy-ready message cards for a round: the post-finalize closing
 * message and the group announcement. Each is shown only when its text is present.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-round-messages',
  imports: [TranslatePipe, Icon],
  template: `
    @if (closingMessage()) {
      <div class="card mb-3 border-success">
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h2 class="h6 fw-bold mb-0">
              <app-icon name="flag" [size]="16" /> {{ 'roundDetail.closingMessage' | translate }}
            </h2>
            <button class="btn btn-sm btn-success" type="button" (click)="copy(closingMessage())">
              <app-icon name="copy" [size]="14" /> {{ 'roundDetail.copy' | translate }}
            </button>
          </div>
          <pre
            class="small mb-0 p-2 copy-message rounded"
            style="white-space: pre-wrap; word-break: break-word"
            >{{ closingMessage() }}</pre
          >
        </div>
      </div>
    }

    @if (groupMessage()) {
      <div class="card mb-3">
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h2 class="h6 fw-bold mb-0">{{ 'roundDetail.groupMessage' | translate }}</h2>
            <button class="btn btn-sm btn-primary" type="button" (click)="copy(groupMessage())">
              <app-icon name="copy" [size]="14" /> {{ 'roundDetail.copy' | translate }}
            </button>
          </div>
          <pre
            class="small mb-0 p-2 copy-message rounded"
            style="white-space: pre-wrap; word-break: break-word"
            >{{ groupMessage() }}</pre
          >
        </div>
      </div>
    }
  `,
})
export class AdminRoundMessages {
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  readonly closingMessage = input('');
  readonly groupMessage = input('');

  protected async copy(text: string): Promise<void> {
    const ok = await copyToClipboard(text);
    this.toast.success(
      this.translate.instant(ok ? 'roundDetail.copied' : 'roundDetail.copyFailed'),
    );
  }
}
