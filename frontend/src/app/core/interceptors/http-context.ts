import { HttpContextToken } from '@angular/common/http';

/** When set on a request, the error interceptor will not show an error toast. */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);
