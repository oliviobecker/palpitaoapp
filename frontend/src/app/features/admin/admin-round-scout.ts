import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { RoundScout } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';
import { copyToClipboard } from '../../shared/utils/clipboard.util';
import { buildMatchScoutMessage } from '../../shared/utils/scout-message.util';

@Component({
  selector: 'app-admin-round-scout',
  imports: [RouterLink, FormsModule, TranslatePipe, EmptyState, Loading],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin/rounds">{{ 'nav.rounds' | translate }}</a> ·
        {{ 'scout.title' | translate }}
      </div>
      <h1 class="h4 fw-bold mb-0">{{ 'scout.title' | translate }}</h1>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (scout(); as s) {
      <p class="text-muted small">{{ 'scout.hint' | translate }}</p>

      @if (matches().length > 0) {
        <div class="mb-3">
          <label class="form-label">{{ 'scout.match' | translate }}</label>
          <div class="input-group input-group-lg">
            <span class="input-group-text">⚽</span>
            <select class="form-select" [(ngModel)]="selectedMatchId">
              @for (m of matches(); track m.roundMatchId) {
                <option [value]="m.roundMatchId">
                  {{ m.homeTeamName }} × {{ m.awayTeamName }}
                </option>
              }
            </select>
          </div>
        </div>

        <div class="card mb-3">
          <div class="card-body">
            <div class="d-flex justify-content-between align-items-center mb-2">
              <h2 class="h6 fw-bold mb-0">{{ 'scout.groupMessage' | translate }}</h2>
              <button class="btn btn-sm btn-primary" type="button" (click)="copy()">
                📋 {{ 'roundDetail.copy' | translate }}
              </button>
            </div>
            <pre
              class="small mb-0 p-2 bg-light rounded border"
              style="white-space: pre-wrap; word-break: break-word"
              >{{ message() }}</pre
            >
          </div>
        </div>
      } @else {
        <app-empty-state [message]="'scout.empty' | translate" />
      }
    }
  `,
})
export class AdminRoundScout implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly adminApi = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly loading = signal(true);
  protected readonly scout = signal<RoundScout | null>(null);
  protected readonly selectedMatchId = signal('');

  protected readonly matches = computed(() => this.scout()?.matches ?? []);
  protected readonly selectedMatch = computed(() => {
    const all = this.matches();
    return all.find((m) => m.roundMatchId === this.selectedMatchId()) ?? all[0] ?? null;
  });
  protected readonly message = computed(() => {
    const m = this.selectedMatch();
    return m ? buildMatchScoutMessage(m) : '';
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.adminApi.getRoundScout(id).subscribe({
      next: (s) => {
        this.scout.set(s);
        this.selectedMatchId.set(s.matches[0]?.roundMatchId ?? '');
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  async copy(): Promise<void> {
    const ok = await copyToClipboard(this.message());
    this.toast.success(
      this.translate.instant(ok ? 'roundDetail.copied' : 'roundDetail.copyFailed'),
    );
  }
}
