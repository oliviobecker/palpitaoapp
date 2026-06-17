import { Location } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { filter } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { Lang, LanguageService } from '../core/i18n/language.service';
import { LoadingService } from '../core/notifications/loading.service';
import { GroupContextService } from '../core/services/group-context.service';
import { APP_BUILD_TIME, APP_COMMIT, APP_VERSION } from '../../version';

/** Top-level tabs reached from the nav — they don't get a back button. */
const ROOT_ROUTES = ['/dashboard', '/rounds', '/standings', '/admin'];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly group = inject(GroupContextService);
  protected readonly loading = inject(LoadingService);
  protected readonly language = inject(LanguageService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  /** Build version shown in the footer; build time goes in the tooltip. */
  protected readonly version = APP_VERSION;
  protected readonly commit = APP_COMMIT && APP_COMMIT !== 'unknown' ? APP_COMMIT : '';
  protected readonly buildInfo = [APP_COMMIT, APP_BUILD_TIME].filter(Boolean).join(' · ');

  private readonly currentUrl = signal(this.router.url);
  /** How many in-app navigations happened — tells us if history.back() is safe. */
  private navCount = 0;

  constructor() {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((e) => {
        this.currentUrl.set(e.urlAfterRedirects);
        this.navCount++;
      });
  }

  /** Show a back button on every page that isn't a top-level tab. */
  protected readonly showBack = computed(() => {
    const url = this.currentUrl().split(/[?#]/)[0];
    return !ROOT_ROUTES.includes(url);
  });

  back(): void {
    if (this.navCount > 1) {
      this.location.back();
    } else {
      // Deep-linked with no in-app history: go to a sensible parent.
      this.router.navigate([this.currentUrl().startsWith('/admin') ? '/admin' : '/dashboard']);
    }
  }

  /** Up to two initials from the signed-in user's name, for the avatar chip. */
  protected readonly initials = computed(() => {
    const name = this.auth.currentUser()?.name?.trim() ?? '';
    if (!name) return '?';
    const parts = name.split(/\s+/);
    const first = parts[0]?.[0] ?? '';
    const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : '';
    return (first + last).toUpperCase();
  });

  logout(): void {
    this.group.clear();
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  /** Drops the current group context and returns to the group chooser. */
  switchGroup(): void {
    this.group.clear();
    this.router.navigate(['/select-group']);
  }

  setLanguage(lang: Lang): void {
    this.language.use(lang);
  }
}
