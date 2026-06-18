import { Component, ChangeDetectionStrategy, input } from '@angular/core';

/**
 * Shared page header: an optional muted trail above an h1 title, with a slot on
 * the right for page-level actions (buttons, pills). Unifies the headers that
 * were previously a mix of bare `<h1>` and ad-hoc `page-trail` + title blocks.
 *
 * - Simple trail: pass the `trail` input (already-translated string).
 * - Rich trail (links): project an element with the `trail` attribute instead.
 * - Right-side actions: project them as default content.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-page-header',
  imports: [],
  template: `
    <div class="d-flex justify-content-between align-items-start gap-2 mb-3 flex-wrap">
      <div class="flex-grow-1" style="min-width: 0">
        @if (trail()) {
          <div class="page-trail">{{ trail() }}</div>
        }
        <ng-content select="[trail]" />
        <h1 class="h4 fw-bold mb-0">{{ title() }}</h1>
      </div>
      <ng-content />
    </div>
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly trail = input<string>('');
}
