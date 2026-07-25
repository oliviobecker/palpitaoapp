import { Injectable, signal } from '@angular/core';

interface ImageViewerState {
  open: boolean;
  src: string;
  title: string;
  subtitle: string;
  downloadName: string;
}

const CLOSED: ImageViewerState = {
  open: false,
  src: '',
  title: '',
  subtitle: '',
  downloadName: '',
};

/**
 * Drives the single root-level image lightbox, mirroring {@link ConfirmService}: any component can
 * open a full-size image without hosting its own overlay.
 */
@Injectable({ providedIn: 'root' })
export class ImageViewerService {
  private readonly _state = signal<ImageViewerState>(CLOSED);
  readonly state = this._state.asReadonly();

  /** `src` is normally a `blob:` object URL produced by {@link OcrImageService}. */
  open(src: string, options?: { title?: string; subtitle?: string; downloadName?: string }): void {
    this._state.set({
      open: true,
      src,
      title: options?.title ?? '',
      subtitle: options?.subtitle ?? '',
      downloadName: options?.downloadName ?? 'image',
    });
  }

  close(): void {
    this._state.set(CLOSED);
  }
}
