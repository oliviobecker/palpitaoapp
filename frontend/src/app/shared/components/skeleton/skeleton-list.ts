import { Component, ChangeDetectionStrategy, computed, input } from '@angular/core';
import { Skeleton } from './skeleton';

/**
 * Loading placeholder for the recurring "vstack of cards" list (rounds, admin
 * lists, standings cards): a tile, two text lines and a trailing pill per row.
 * Mirrors the real card-row so the page keeps its shape while data loads.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-skeleton-list',
  imports: [Skeleton],
  template: `
    <div class="vstack gap-2" role="status" aria-busy="true">
      @for (i of rows(); track i) {
        <div class="card">
          <div class="card-body d-flex align-items-center gap-3">
            <app-skeleton width="3rem" height="3rem" radius="12px" />
            <div class="flex-grow-1" style="min-width: 0">
              <app-skeleton width="60%" height="0.9rem" />
              <span class="d-block mt-2"><app-skeleton width="35%" height="0.7rem" /></span>
            </div>
            <app-skeleton width="4.5rem" height="1.5rem" radius="999px" />
          </div>
        </div>
      }
    </div>
  `,
})
export class SkeletonList {
  readonly count = input<number>(4);
  protected readonly rows = computed(() => Array.from({ length: this.count() }, (_, i) => i));
}
