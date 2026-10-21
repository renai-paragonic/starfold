import { beforeEach, describe, expect, it } from 'vitest';
import { openDb, type Db } from '../src/db/index.js';
import { addCommand, deleteCommand, getCommand, listCommands, recordUse, updateCommand } from '../src/db/commands.js';

let db: Db;
beforeEach(() => {
  db = openDb(':memory:');
});

const hello = { name: 'hello', response: 'Hi {user}!', permission: 'everyone' as const, cooldownS: 5, createdBy: 'mod1' };

describe('commands repo', () => {
  it('adds and reads back a command with defaults', () => {
    expect(addCommand(db, hello, 1000)).toBe(true);
    expect(getCommand(db, 'hello')).toEqual({
      ...hello,
      enabled: true,
      useCount: 0,
      updatedAt: 1000,
    });
  });

  it('refuses a duplicate name', () => {
    addCommand(db, hello);
    expect(addCommand(db, { ...hello, response: 'other' })).toBe(false);
    expect(getCommand(db, 'hello')?.response).toBe('Hi {user}!');
  });

  it('updates only the given fields', () => {
    addCommand(db, hello, 1000);
    expect(updateCommand(db, 'hello', { cooldownS: 30 }, 2000)).toBe(true);
    expect(getCommand(db, 'hello')).toMatchObject({ response: 'Hi {user}!', permission: 'everyone', cooldownS: 30, updatedAt: 2000 });

    updateCommand(db, 'hello', { response: 'Hey', permission: 'subscriber', cooldownS: 0 });
    expect(getCommand(db, 'hello')).toMatchObject({ response: 'Hey', permission: 'subscriber', cooldownS: 0 });
  });

  it('reports missing commands on update and delete', () => {
    expect(updateCommand(db, 'nope', { response: 'x' })).toBe(false);
    expect(deleteCommand(db, 'nope')).toBe(false);
  });

  it('deletes and lists by name', () => {
    addCommand(db, { ...hello, name: 'zed' });
    addCommand(db, hello);
    expect(listCommands(db).map((c) => c.name)).toEqual(['hello', 'zed']);
    expect(deleteCommand(db, 'zed')).toBe(true);
    expect(listCommands(db).map((c) => c.name)).toEqual(['hello']);
  });

  it('counts uses', () => {
    addCommand(db, hello);
    expect(recordUse(db, 'hello')).toBe(1);
    expect(recordUse(db, 'hello')).toBe(2);
    expect(getCommand(db, 'hello')?.useCount).toBe(2);
  });
});
