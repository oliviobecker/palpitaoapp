import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { Lang, LanguageService } from '../../core/i18n/language.service';
import { ThemeService } from '../../core/theme/theme.service';
import { Icon } from '../../shared/components/icon/icon';

/**
 * Public marketing landing page shown at the root path to signed-out visitors.
 * The language follows the browser by default (resolved by LanguageService at
 * app startup) and can be switched here; the theme toggle mirrors the in-app
 * shell. Signed-in visitors are sent straight into the app.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-landing',
  imports: [RouterLink, TranslatePipe, Icon],
  templateUrl: './landing.html',
  styleUrl: './landing.scss',
})
export class Landing {
  protected readonly language = inject(LanguageService);
  protected readonly theme = inject(ThemeService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly year = new Date().getFullYear();

  constructor() {
    // Signed-in visitors don't need the marketing page — drop them into the app
    // (the participant guard forwards to group selection when no group is set).
    if (this.auth.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    }
  }

  setLanguage(lang: Lang): void {
    this.language.use(lang);
  }
}
