import { describe, expect, it } from 'vitest';
import { RoundLabelPipe } from './round-label.pipe';

describe('RoundLabelPipe', () => {
  const pipe = new RoundLabelPipe();

  it('labels a standalone round and a part', () => {
    expect(pipe.transform(10)).toBe('10');
    expect(pipe.transform(10, 0)).toBe('10');
    expect(pipe.transform(10, 2)).toBe('10.2');
  });

  it('renders nothing while the round has not loaded', () => {
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform(null, 2)).toBe('');
  });
});
