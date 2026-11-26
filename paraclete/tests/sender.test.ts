import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createSendQueue } from '../src/twitch/sender.js';

beforeEach(() => {
  vi.useFakeTimers();
});
afterEach(() => {
  vi.useRealTimers();
});

describe('createSendQueue', () => {
  it('sends in order and holds messages beyond the limit until the window slides', async () => {
    const sent: string[] = [];
    const q = createSendQueue<string>(async (s) => void sent.push(s), {
      limit: 2,
      windowMs: 1000,
      onError: () => {},
    });

    q.push('a');
    q.push('b');
    q.push('c');
    await vi.advanceTimersByTimeAsync(0);
    expect(sent).toEqual(['a', 'b']);

    await vi.advanceTimersByTimeAsync(999);
    expect(sent).toEqual(['a', 'b']);

    await vi.advanceTimersByTimeAsync(1);
    expect(sent).toEqual(['a', 'b', 'c']);
  });

  it('keeps going after a failed send', async () => {
    const sent: string[] = [];
    const errors: string[] = [];
    const q = createSendQueue<string>(
      async (s) => {
        if (s === 'bad') throw new Error('boom');
        sent.push(s);
      },
      { limit: 10, windowMs: 1000, onError: (_err, item) => errors.push(item) },
    );

    q.push('bad');
    q.push('good');
    await vi.advanceTimersByTimeAsync(0);
    expect(errors).toEqual(['bad']);
    expect(sent).toEqual(['good']);
  });

  it('counts failed sends against the limit', async () => {
    const attempts: string[] = [];
    const q = createSendQueue<string>(
      async (s) => {
        attempts.push(s);
        throw new Error('boom');
      },
      { limit: 1, windowMs: 1000, onError: () => {} },
    );

    q.push('a');
    q.push('b');
    await vi.advanceTimersByTimeAsync(0);
    expect(attempts).toEqual(['a']);
    await vi.advanceTimersByTimeAsync(1000);
    expect(attempts).toEqual(['a', 'b']);
  });

  it('resumes after going idle', async () => {
    const sent: string[] = [];
    const q = createSendQueue<string>(async (s) => void sent.push(s), {
      limit: 5,
      windowMs: 1000,
      onError: () => {},
    });

    q.push('a');
    await vi.advanceTimersByTimeAsync(0);
    q.push('b');
    await vi.advanceTimersByTimeAsync(0);
    expect(sent).toEqual(['a', 'b']);
  });
});
