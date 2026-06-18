import { registerLocaleData } from '@angular/common';
import localeEn from '@angular/common/locales/en';
import localePt from '@angular/common/locales/pt';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import {
  provideLucideIcons,
  LucideArrowLeft,
  LucideArrowLeftRight,
  LucideArrowRight,
  LucideBan,
  LucideCalendarDays,
  LucideChartColumn,
  LucideCheck,
  LucideCircle,
  LucideCircleCheck,
  LucideClipboardList,
  LucideClock,
  LucideEye,
  LucideFilePen,
  LucideFlag,
  LucideHourglass,
  LucideHouse,
  LucideInbox,
  LucideList,
  LucideLock,
  LucideLogOut,
  LucideMail,
  LucideMoon,
  LucidePencil,
  LucidePlane,
  LucidePlay,
  LucidePlus,
  LucideRefreshCw,
  LucideScrollText,
  LucideSearch,
  LucideSettings,
  LucideShuffle,
  LucideStar,
  LucideSun,
  LucideTag,
  LucideTarget,
  LucideTriangleAlert,
  LucideTrophy,
  LucideUser,
  LucideUsers,
  LucideX,
  LucideCopy,
  LucideGoal,
  LucideImage,
  LucideLanguages,
  LucideLightbulb,
  LucideReceipt,
  LucideTimer,
  LucideTrash2,
  LucideUndo2,
} from '@lucide/angular';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { groupInterceptor } from './core/interceptors/group.interceptor';
import { languageInterceptor } from './core/interceptors/language.interceptor';
import { loadingInterceptor } from './core/interceptors/loading.interceptor';

registerLocaleData(localePt);
registerLocaleData(localeEn);

// Translation JSON files are not content-hashed like the JS bundles, so browsers
// (and proxies/IIS) can serve a stale copy after a deploy that added new keys —
// which shows raw keys like "register.title". Append a per-load token so each app
// load fetches the current files. The files are tiny, so the extra fetch is cheap.
const I18N_VERSION = Date.now();

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([
        authInterceptor,
        groupInterceptor,
        languageInterceptor,
        errorInterceptor,
        loadingInterceptor,
      ]),
    ),
    provideTranslateService({
      loader: provideTranslateHttpLoader({
        prefix: '/i18n/',
        suffix: `.json?v=${I18N_VERSION}`,
        useHttpBackend: true,
      }),
      fallbackLang: 'en-US',
    }),
    { provide: LOCALE_ID, useValue: 'pt-BR' },
    // Lucide icons used across the UI, registered by lower-kebab-case name and
    // rendered through the <app-icon name="..."> wrapper.
    provideLucideIcons(
      LucideArrowLeft,
      LucideArrowLeftRight,
      LucideArrowRight,
      LucideBan,
      LucideCalendarDays,
      LucideChartColumn,
      LucideCheck,
      LucideCircle,
      LucideCircleCheck,
      LucideClipboardList,
      LucideClock,
      LucideEye,
      LucideFilePen,
      LucideFlag,
      LucideHourglass,
      LucideHouse,
      LucideInbox,
      LucideList,
      LucideLock,
      LucideLogOut,
      LucideMail,
      LucideMoon,
      LucidePencil,
      LucidePlane,
      LucidePlay,
      LucidePlus,
      LucideRefreshCw,
      LucideScrollText,
      LucideSearch,
      LucideSettings,
      LucideShuffle,
      LucideStar,
      LucideSun,
      LucideTag,
      LucideTarget,
      LucideTriangleAlert,
      LucideTrophy,
      LucideUser,
      LucideUsers,
      LucideX,
      LucideCopy,
      LucideGoal,
      LucideImage,
      LucideLanguages,
      LucideLightbulb,
      LucideReceipt,
      LucideTimer,
      LucideTrash2,
      LucideUndo2,
    ),
  ],
};
