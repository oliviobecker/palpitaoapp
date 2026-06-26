import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { ConfirmService } from '../notifications/confirm.service';

/** Implemented by components that want to warn before navigating away with unsaved edits. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

/**
 * Route guard that prompts for confirmation when leaving a page that still has
 * unsaved changes. Reuses the shared {@link ConfirmService}.
 */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) => {
  if (!component.hasUnsavedChanges()) {
    return true;
  }
  const confirm = inject(ConfirmService);
  const translate = inject(TranslateService);
  return confirm.ask(translate.instant('common.unsavedConfirm'), {
    title: translate.instant('common.unsavedTitle'),
    confirmText: translate.instant('common.leave'),
    danger: true,
  });
};
