import { describe, expect, it } from 'vitest';
import { migrate, openDb } from '../src/db/index.js';
import { migrations } from '../src/db/migrations.js';

const userVersion = (db: ReturnType<typeof openDb>) =>
  Number(db.prepare('PRAGMA user_version').get()?.user_version);

describe('openDb', () => {
  it('creates the schema and records the version', () => {
    const db = openDb(':memory:');
    const tables = db
      .prepare("SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name")
      .all()
      .map((r) => r.name);
    expect(tables).toEqual(['commands', 'event_responses', 'tokens']);
    expect(userVersion(db)).toBe(migrations.length);
  });

  it('seeds default event responses', () => {
    const db = openDb(':memory:');
    const types = db
      .prepare('SELECT event_type FROM event_responses WHERE enabled = 1 ORDER BY event_type')
      .all()
      .map((r) => r.event_type);
    expect(types).toEqual(['follow', 'gift', 'raid', 'redeem', 'resub', 'sub']);
  });

  it('is a no-op when migrated again', () => {
    const db = openDb(':memory:');
    migrate(db);
    expect(userVersion(db)).toBe(migrations.length);
    expect(db.prepare('SELECT COUNT(*) AS n FROM event_responses').get()?.n).toBe(6);
  });

  it('allows only one token per role', () => {
    const db = openDb(':memory:');
    const insert = db.prepare(
      "INSERT INTO tokens (user_id, role, access_token, scopes, obtained_at) VALUES (?, 'bot', 'a', '[]', 0)",
    );
    insert.run('1');
    expect(() => insert.run('2')).toThrow(/UNIQUE/);
  });

  it('rolls back a failing migration', () => {
    const db = openDb(':memory:');
    migrations.push('CREATE TABLE half (x); SELECT * FROM missing_table;');
    try {
      expect(() => migrate(db)).toThrow();
    } finally {
      migrations.pop();
    }
    expect(userVersion(db)).toBe(migrations.length);
    expect(db.prepare("SELECT name FROM sqlite_master WHERE name = 'half'").get()).toBeUndefined();
  });
});
