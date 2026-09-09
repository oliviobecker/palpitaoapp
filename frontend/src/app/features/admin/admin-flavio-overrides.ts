import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { RoundStatus } from '../../core/models/enums';
import { Round } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import {
  AdminService,
  FlavioParticipant,
  RoundFlavioOverrides,
} from '../../core/services/admin.service';
import { Skeleton } from '../../shared/components/skeleton/skeleton';

@Component({
  selector: 'app-admin-flavio-overrides',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, Skeleton],
  templateUrl: './admin-flavio-overrides.html',
  styles: [
    `
      @media (max-width: 767.98px) {
        table,
        tbody,
        tr,
        th,
        td {
          display: block;
        }
        thead {
          display: none;
        }
        tr {
          margin-bottom: 1rem;
          border-bottom: 1px solid var(--bs-border-color);
        }
        th,
        td {
          border: 0;
        }
        td::before {
          content: attr(data-label);
          display: block;
          font-size: 0.8rem;
          font-weight: 600;
        }
        th .badge {
          display: table;
          margin-top: 0.25rem;
        }
        button {
          width: 100%;
        }
      }
    `,
  ],
})
export class AdminFlavioOverrides implements OnInit {
  readonly round = input.required<Round>();
  readonly disabled = input(false);
  readonly saved = output<void>();
  readonly busyChange = output<boolean>();
  protected readonly data = signal<RoundFlavioOverrides | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  private readonly api = inject(AdminService);
  private readonly confirm = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api
      .getFlavioOverrides(this.round().id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.data.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.failed.set(true);
        },
      });
  }

  async change(participant: FlavioParticipant): Promise<void> {
    if (this.disabled()) return;
    const scored = this.round().status === RoundStatus.Scored;
    const action = participant.isExempt ? 'flavioOverride.restore' : 'flavioOverride.exempt';
    const justification = await this.confirm.askWithInput(
      this.translate.instant(
        scored ? 'flavioOverride.confirmRecalculate' : 'flavioOverride.confirmSave',
        {
          name: participant.name,
        },
      ),
      {
        title: this.translate.instant(action),
        confirmText: this.translate.instant(
          scored ? 'flavioOverride.saveRecalculate' : 'flavioOverride.save',
        ),
        inputLabel: this.translate.instant('flavioOverride.justification'),
        required: true,
      },
    );
    if (justification === null) return;
    if (!justification.trim() || justification.length > 500) {
      this.toast.error(this.translate.instant('flavioOverride.invalidJustification'));
      return;
    }
    this.busyChange.emit(true);
    this.api
      .setFlavioOverride(this.round().id, {
        userId: participant.userId,
        isExempt: !participant.isExempt,
        justification,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.busyChange.emit(false);
          this.toast.success(
            this.translate.instant(scored ? 'flavioOverride.recalculated' : 'flavioOverride.saved'),
          );
          this.saved.emit();
        },
        error: () => this.busyChange.emit(false),
      });
  }
}
