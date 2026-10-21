import { describe, expect, it, vi } from 'vitest';
import { formatAge, formatUptime, render, type LiveData, type TemplateVars } from '../src/core/commands/templates.js';

const vars: TemplateVars = {
  user: 'Alice',
  target: 'Bob',
  targetLogin: 'bob',
  args: ['@Bob', 'is', 'cool'],
  channel: 'Streamer',
  count: 7,
};

const live = (): LiveData => ({
  uptime: vi.fn(async () => '1h 5m'),
  viewers: vi.fn(async () => '42'),
  game: vi.fn(async () => 'Hollow Knight'),
  title: vi.fn(async () => 'Steel Soul'),
  followage: vi.fn(async (login: string) => `age-of-${login}`),
  gameOf: vi.fn(async (login: string) => `game-of-${login}`),
});

describe('render', () => {
  it('fills message-based placeholders', async () => {
    const out = await render('{user} hugs {target} in {channel} (#{count}): {args} / {arg2} / [{arg9}]', vars, live());
    expect(out).toBe('Alice hugs Bob in Streamer (#7): @Bob is cool / is / []');
  });

  it('fills live placeholders and only calls what is used', async () => {
    const l = live();
    const out = await render('{game} | {title} | {uptime} | {viewers} | {followage} | {targetgame}', vars, l);
    expect(out).toBe('Hollow Knight | Steel Soul | 1h 5m | 42 | age-of-bob | game-of-bob');
    expect(l.followage).toHaveBeenCalledWith('bob');
    expect(l.gameOf).toHaveBeenCalledWith('bob');

    const l2 = live();
    await render('{user}', vars, l2);
    expect(l2.uptime).not.toHaveBeenCalled();
  });

  it('picks random numbers and options with the given rng', async () => {
    expect(await render('{random:1-100}', vars, live(), () => 0)).toBe('1');
    expect(await render('{random:1-100}', vars, live(), () => 0.9999)).toBe('100');
    expect(await render('{random:10-1}', vars, live(), () => 0)).toBe('1');
    expect(await render('{pick:heads|tails}', vars, live(), () => 0.6)).toBe('tails');
  });

  it('leaves unknown and malformed placeholders alone', async () => {
    expect(await render('{nope} {random:abc} {pick:} {user', vars, live())).toBe('{nope} {random:abc} {pick:} {user');
  });

  it('never expands placeholders that came from user input', async () => {
    const l = live();
    const out = await render('You said: {args}', { ...vars, args: ['{uptime}', '{user}'] }, l);
    expect(out).toBe('You said: {uptime} {user}');
    expect(l.uptime).not.toHaveBeenCalled();
  });
});

describe('formatUptime', () => {
  it('shows hours and minutes', () => {
    expect(formatUptime(0)).toBe('0m');
    expect(formatUptime(59 * 60_000)).toBe('59m');
    expect(formatUptime((2 * 60 + 14) * 60_000 + 59_000)).toBe('2h 14m');
  });
});

describe('formatAge', () => {
  const d = (s: string) => new Date(`${s}T12:00:00Z`);

  it('uses the two largest units', () => {
    expect(formatAge(d('2025-07-10'), d('2026-10-20'))).toBe('1 year, 3 months');
    expect(formatAge(d('2026-07-10'), d('2026-10-20'))).toBe('3 months, 10 days');
    expect(formatAge(d('2024-10-20'), d('2026-10-20'))).toBe('2 years');
    expect(formatAge(d('2026-10-19'), d('2026-10-20'))).toBe('1 day');
    expect(formatAge(d('2026-10-20'), d('2026-10-20'))).toBe('less than a day');
  });

  it('handles month ends', () => {
    expect(formatAge(d('2026-01-31'), d('2026-03-01'))).toBe('1 month, 1 day');
    // Adding a month to Jan 31 clamps to the last day of February.
    expect(formatAge(d('2026-01-31'), d('2026-02-28'))).toBe('1 month');
    expect(formatAge(d('2026-01-31'), d('2026-02-27'))).toBe('27 days');
  });
});
