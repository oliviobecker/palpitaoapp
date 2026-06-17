import { HttpErrorResponse } from '@angular/common/http';

/** Extracts a friendly (Portuguese) message from an API error response. */
export function httpErrorMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'Não foi possível conectar ao servidor. Verifique se a API está no ar.';
  }
  if (error.status === 401) {
    return 'E-mail ou senha inválidos.';
  }

  const body = error.error;
  if (body && typeof body === 'object') {
    if (typeof body.message === 'string') {
      return body.message;
    }
    if (body.errors) {
      const first = Object.values(body.errors)[0];
      if (Array.isArray(first) && first.length) {
        return String(first[0]);
      }
    }
  }

  return 'Ocorreu um erro inesperado. Tente novamente.';
}
