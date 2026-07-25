import { describe, expect, it } from 'vitest';
import { ImageViewerService } from './image-viewer.service';

describe('ImageViewerService', () => {
  it('opens with the given source and metadata', () => {
    const svc = new ImageViewerService();
    svc.open('blob:abc', {
      title: 'palpites.png',
      subtitle: 'Sent by Ana',
      downloadName: 'palpites.png',
    });

    expect(svc.state().open).toBe(true);
    expect(svc.state().src).toBe('blob:abc');
    expect(svc.state().title).toBe('palpites.png');
    expect(svc.state().subtitle).toBe('Sent by Ana');
    expect(svc.state().downloadName).toBe('palpites.png');
  });

  it('falls back to a generic download name', () => {
    const svc = new ImageViewerService();
    svc.open('blob:abc');

    expect(svc.state().downloadName).toBe('image');
    expect(svc.state().title).toBe('');
  });

  it('clears the state on close', () => {
    const svc = new ImageViewerService();
    svc.open('blob:abc', { title: 'x.png' });
    svc.close();

    expect(svc.state().open).toBe(false);
    expect(svc.state().src).toBe('');
  });
});
