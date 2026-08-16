import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { OcrParticipantAlias, Participant } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';

/** Filters by the name as it was read and by the participant it points at. */
export function filterAliases(
  aliases: OcrParticipantAlias[],
  query: string,
): OcrParticipantAlias[] {
  const term = query.trim().toLowerCase();
  if (!term) {
    return aliases;
  }

  return aliases.filter(
    (a) =>
      a.aliasRaw.toLowerCase().includes(term) ||
      a.alias.includes(term) ||
      a.userName.toLowerCase().includes(term),
  );
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-ocr-aliases',
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    TranslatePipe,
    EmptyState,
    ErrorState,
    Icon,
    PageHeader,
    SkeletonList,
  ],
  templateUrl: './admin-ocr-aliases.html',
  styleUrl: './admin-ocr-aliases.scss',
})
export class AdminOcrAliases implements OnInit {
  private readonly api = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly aliases = signal<OcrParticipantAlias[]>([]);
  protected readonly participants = signal<Participant[]>([]);
  protected readonly search = signal('');
  /** Id of the row being written, so only its own controls are disabled. */
  protected readonly busyId = signal<string | null>(null);
  protected readonly creating = signal(false);

  protected newAlias = '';
  protected newUserId: string | null = null;

  protected readonly visible = computed(() => filterAliases(this.aliases(), this.search()));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    forkJoin({
      aliases: this.api.listOcrAliases(),
      participants: this.api.listParticipants(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ aliases, participants }) => {
          this.aliases.set(aliases);
          this.participants.set(participants);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  create(): void {
    const raw = this.newAlias.trim();
    if (!raw || !this.newUserId) {
      return;
    }
    this.creating.set(true);
    this.api
      .createOcrAlias(raw, this.newUserId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (created) => {
          this.aliases.update((list) =>
            [...list, created].sort((a, b) => a.aliasRaw.localeCompare(b.aliasRaw)),
          );
          this.newAlias = '';
          this.newUserId = null;
          this.creating.set(false);
          this.toast.success(
            this.translate.instant('ocrAliases.added', {
              alias: created.aliasRaw,
              name: created.userName,
            }),
          );
        },
        // A duplicate or an unknown participant already reached the user through the
        // interceptor's toast; the form keeps what was typed so it can be corrected.
        error: () => this.creating.set(false),
      });
  }

  repoint(alias: OcrParticipantAlias, userId: string): void {
    if (userId === alias.userId) {
      return;
    }
    this.busyId.set(alias.id);
    this.api
      .updateOcrAlias(alias.id, userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.aliases.update((list) => list.map((a) => (a.id === updated.id ? updated : a)));
          this.busyId.set(null);
          this.toast.success(
            this.translate.instant('ocrAliases.updated', {
              alias: updated.aliasRaw,
              name: updated.userName,
            }),
          );
        },
        error: () => {
          this.busyId.set(null);
          this.load();
        },
      });
  }

  async remove(alias: OcrParticipantAlias): Promise<void> {
    const ok = await this.confirmDialog.ask(
      this.translate.instant('ocrAliases.confirmRemove', { alias: alias.aliasRaw }),
      {
        title: this.translate.instant('ocrAliases.remove'),
        confirmText: this.translate.instant('ocrAliases.remove'),
        danger: true,
      },
    );
    if (!ok) {
      return;
    }
    this.busyId.set(alias.id);
    this.api
      .deleteOcrAlias(alias.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.aliases.update((list) => list.filter((a) => a.id !== alias.id));
          this.busyId.set(null);
          this.toast.success(this.translate.instant('ocrAliases.removed'));
        },
        error: () => this.busyId.set(null),
      });
  }
}
