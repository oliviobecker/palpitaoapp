import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/i18n/language.service';
import { ConfirmDialog } from './shared/components/confirm-dialog/confirm-dialog';
import { ToastContainer } from './shared/components/toast-container/toast-container';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainer, ConfirmDialog],
  // Toast + confirm live at the root so they are visible on every page (incl. login).
  template: '<router-outlet /><app-toast-container /><app-confirm-dialog />',
})
export class App {
  constructor() {
    // Detect and apply the browser language at startup.
    inject(LanguageService).init();
  }
}
