import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/i18n/language.service';
import { ThemeService } from './core/theme/theme.service';
import { ConfirmDialog } from './shared/components/confirm-dialog/confirm-dialog';
import { ImageViewer } from './shared/components/image-viewer/image-viewer';
import { ToastContainer } from './shared/components/toast-container/toast-container';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainer, ConfirmDialog, ImageViewer],
  // Toast + confirm + image viewer live at the root so they are visible on every page (incl. login).
  template: '<router-outlet /><app-toast-container /><app-confirm-dialog /><app-image-viewer />',
})
export class App {
  constructor() {
    // Detect and apply the browser language at startup.
    inject(LanguageService).init();
    // Resolve the light/dark theme (stored override or OS preference).
    inject(ThemeService).init();
  }
}
