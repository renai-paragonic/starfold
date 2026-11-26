import type { AccessToken } from '@twurple/auth';
import { beforeEach, describe, expect, it } from 'vitest';
import { openDb, type Db } from '../src/db/index.js';
import { loadTokens, saveToken, updateToken } from '../src/db/tokens.js';

const token = (n: number): AccessToken => ({
  accessToken: `access-${n}`,
  refreshToken: `refresh-${n}`,
  scope: ['user:read:chat', 'user:bot'],
  expiresIn: 14400,
  obtainmentTimestamp: 1_000 * n,
});

let db: Db;
beforeEach(() => {
  db = openDb(':memory:');
});

describe('tokens repo', () => {
  it('round-trips a token', () => {
    saveToken(db, 'bot', '111', token(1));
    expect(loadTokens(db)).toEqual([{ role: 'bot', userId: '111', token: token(1) }]);
  });

  it('keeps null refresh token and expiry', () => {
    const t = { ...token(1), refreshToken: null, expiresIn: null };
    saveToken(db, 'bot', '111', t);
    expect(loadTokens(db)[0]?.token).toEqual(t);
  });

  it('replaces the token when a role is re-authorized, even by another account', () => {
    saveToken(db, 'bot', '111', token(1));
    saveToken(db, 'bot', '222', token(2));
    expect(loadTokens(db)).toEqual([{ role: 'bot', userId: '222', token: token(2) }]);
  });

  it('refuses the same account for both roles', () => {
    saveToken(db, 'bot', '111', token(1));
    expect(() => saveToken(db, 'broadcaster', '111', token(2))).toThrow();
  });

  it('updates a refreshed token by user id', () => {
    saveToken(db, 'bot', '111', token(1));
    saveToken(db, 'broadcaster', '222', token(2));
    updateToken(db, '111', token(3));
    expect(loadTokens(db)).toEqual([
      { role: 'bot', userId: '111', token: token(3) },
      { role: 'broadcaster', userId: '222', token: token(2) },
    ]);
  });

  it('throws when refreshing an unknown user', () => {
    expect(() => updateToken(db, '999', token(1))).toThrow(/999/);
  });
});
