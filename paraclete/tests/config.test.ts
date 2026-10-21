import { describe, expect, it } from 'vitest';
import { loadConfig } from '../src/config.js';

const base = {
  TWITCH_CLIENT_ID: 'id',
  TWITCH_CLIENT_SECRET: 'secret',
  TWITCH_CHANNEL: 'MyChannel',
};

describe('loadConfig', () => {
  it('applies defaults and normalizes the channel', () => {
    expect(loadConfig(base)).toEqual({
      clientId: 'id',
      clientSecret: 'secret',
      channel: 'mychannel',
      dbPath: './data/paraclete.db',
      logLevel: 'info',
      chatRateLimit: 20,
    });
  });

  it('respects overrides', () => {
    const cfg = loadConfig({ ...base, DB_PATH: '/data/paraclete.db', LOG_LEVEL: 'debug', CHAT_RATE_LIMIT: '100' });
    expect(cfg.dbPath).toBe('/data/paraclete.db');
    expect(cfg.logLevel).toBe('debug');
    expect(cfg.chatRateLimit).toBe(100);
  });

  it('names missing variables in the error', () => {
    expect(() => loadConfig({ TWITCH_CLIENT_ID: 'id' })).toThrow(/TWITCH_CLIENT_SECRET/);
  });

  it('rejects an unknown log level', () => {
    expect(() => loadConfig({ ...base, LOG_LEVEL: 'loud' })).toThrow(/LOG_LEVEL/);
  });
});
