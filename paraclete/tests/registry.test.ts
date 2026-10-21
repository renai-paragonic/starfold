import { beforeEach, describe, expect, it } from 'vitest';
import type { ChatMessage } from '../src/core/chat.js';
import { levelOf, parsePermission } from '../src/core/commands/permissions.js';
import { createCommandHandler, parseCommand, USER_COOLDOWN_S } from '../src/core/commands/registry.js';
import type { LiveData } from '../src/core/commands/templates.js';
import { addCommand, getCommand, updateCommand } from '../src/db/commands.js';
import { openDb, type Db } from '../src/db/index.js';

const live: LiveData = {
  uptime: async () => '1h',
  viewers: async () => '1',
  game: async () => 'Game',
  title: async () => 'Title',
  followage: async () => '1 year',
  gameOf: async () => 'Other Game',
};

const VIEWER = {};
const SUB = { subscriber: '12' };
const MOD = { moderator: '1' };

let db: Db;
let now: number;
let run: (text: string, badges?: Record<string, string>, userId?: string) => Promise<string[]>;

beforeEach(() => {
  db = openDb(':memory:');
  now = 1_000_000;
  const handle = createCommandHandler({ db, live, now: () => now, random: () => 0 });
  run = async (text, badges = VIEWER, userId = 'u1') => {
    const msg: ChatMessage = {
      id: 'm',
      text,
      userId,
      userLogin: `login_${userId}`,
      userDisplayName: `Name_${userId}`,
      badges,
      channel: 'Streamer',
    };
    const replies: string[] = [];
    await handle(msg, (t) => replies.push(t));
    return replies;
  };
});

describe('parseCommand', () => {
  it('splits name and args', () => {
    expect(parseCommand('  !Hello  @bob   hi ')).toEqual({ name: 'hello', args: ['@bob', 'hi'] });
  });
  it('ignores non-commands', () => {
    expect(parseCommand('hello !ping')).toBeNull();
    expect(parseCommand('!')).toBeNull();
    expect(parseCommand('')).toBeNull();
  });
});

describe('permissions', () => {
  it('reads the highest level from badges', () => {
    expect(levelOf({})).toBe('everyone');
    expect(levelOf({ founder: '0' })).toBe('subscriber');
    expect(levelOf({ vip: '1', subscriber: '3' })).toBe('vip');
    expect(levelOf({ moderator: '1', subscriber: '3' })).toBe('moderator');
    expect(levelOf({ broadcaster: '1', subscriber: '3' })).toBe('broadcaster');
  });
  it('accepts aliases', () => {
    expect(parsePermission('Sub')).toBe('subscriber');
    expect(parsePermission('mod')).toBe('moderator');
    expect(parsePermission('toString')).toBeUndefined();
  });
});

describe('command handler', () => {
  it('answers !ping', async () => {
    expect(await run('!ping')).toEqual(['pong']);
  });

  it('ignores unknown commands and inherited object keys', async () => {
    expect(await run('!nope')).toEqual([]);
    expect(await run('!constructor')).toEqual([]);
  });

  it('runs a custom command with templates and counts uses', async () => {
    await run('!addcmd !hug {user} hugs {target} (hug #{count}) for {followage}', MOD);
    expect(await run('!hug @Bob', VIEWER, 'u1')).toEqual(['Name_u1 hugs Bob (hug #1) for 1 year']);
    expect(await run('!hug', VIEWER, 'u2')).toEqual([]); // still on the 5s command cooldown
    now += 5000;
    expect(await run('!hug', VIEWER, 'u2')).toEqual(['Name_u2 hugs Name_u2 (hug #2) for 1 year']);
  });

  it('enforces permission levels', async () => {
    await run('!addcmd -perm=sub !perk Sub perk', MOD);
    expect(await run('!perk', VIEWER)).toEqual([]);
    expect(await run('!perk', SUB)).toEqual(['Sub perk']);
    expect(await run('!perk', MOD, 'u2')).toEqual(['Sub perk']);
    expect(await run('!addcmd !x y', SUB)).toEqual([]);
  });

  it('applies command and per-user cooldowns, which mods skip', async () => {
    await run('!addcmd -cd=0 !free ok', MOD);
    expect(await run('!free', VIEWER, 'u1')).toEqual(['ok']);
    expect(await run('!free', VIEWER, 'u2')).toEqual(['ok']); // no command cooldown
    expect(await run('!free', VIEWER, 'u1')).toEqual([]); // per-user cooldown
    expect(await run('!free', MOD, 'u3')).toEqual(['ok']);
    expect(await run('!free', MOD, 'u3')).toEqual(['ok']);
    now += USER_COOLDOWN_S * 1000;
    expect(await run('!free', VIEWER, 'u1')).toEqual(['ok']);
  });

  it('skips disabled commands', async () => {
    await run('!addcmd !off nope', MOD);
    db.prepare("UPDATE commands SET enabled = 0 WHERE name = 'off'").run();
    expect(await run('!off')).toEqual([]);
  });

  it('truncates replies to 500 characters', async () => {
    addCommand(db, { name: 'long', response: 'x'.repeat(600), permission: 'everyone', cooldownS: 0, createdBy: 'm' });
    expect((await run('!long'))[0]).toHaveLength(500);
  });
});

describe('management builtins', () => {
  it('adds with defaults, flags and creator', async () => {
    expect(await run('!addcmd !hello Hi there {user}', MOD)).toEqual(['Added !hello.']);
    expect(getCommand(db, 'hello')).toMatchObject({
      response: 'Hi there {user}',
      permission: 'everyone',
      cooldownS: 5,
      createdBy: 'login_u1',
    });
    expect(await run('!addcmd -perm=vip -cd=30 vipcmd hey', MOD)).toEqual(['Added !vipcmd.']);
    expect(getCommand(db, 'vipcmd')).toMatchObject({ permission: 'vip', cooldownS: 30 });
  });

  it('rejects bad input', async () => {
    expect((await run('!addcmd', MOD))[0]).toMatch(/^Usage: !addcmd/);
    expect((await run('!addcmd !only', MOD))[0]).toMatch(/^Usage: !addcmd/);
    expect((await run('!addcmd -perm=king !x y', MOD))[0]).toMatch(/^Unknown option -perm=king/);
    expect((await run('!addcmd -cd=abc !x y', MOD))[0]).toMatch(/^Unknown option/);
    expect(await run('!addcmd !bad-name y', MOD)).toEqual(['Command names use letters, numbers and _ only.']);
    expect(await run('!addcmd !ping hijack', MOD)).toEqual(['!ping is a built-in command.']);
    await run('!addcmd !dup one', MOD);
    expect(await run('!addcmd !dup two', MOD)).toEqual(['!dup already exists; use !editcmd.']);
  });

  it('edits response and options', async () => {
    await run('!addcmd !hello Hi', MOD);
    expect(await run('!editcmd !hello Hey {user}', MOD)).toEqual(['Updated !hello.']);
    expect(await run('!editcmd -perm=mod -cd=0 !hello', MOD)).toEqual(['Updated !hello.']);
    expect(getCommand(db, 'hello')).toMatchObject({ response: 'Hey {user}', permission: 'moderator', cooldownS: 0 });
    expect(await run('!editcmd !missing x', MOD)).toEqual(["!missing doesn't exist."]);
    expect((await run('!editcmd !hello', MOD))[0]).toMatch(/^Usage: !editcmd/);
    expect(await run('!editcmd !ping x', MOD)).toEqual(['!ping is a built-in command.']);
  });

  it('deletes', async () => {
    await run('!addcmd !bye Bye', MOD);
    expect(await run('!delcmd !bye', MOD)).toEqual(['Deleted !bye.']);
    expect(getCommand(db, 'bye')).toBeUndefined();
    expect(await run('!delcmd bye', MOD)).toEqual(["!bye doesn't exist."]);
    expect(await run('!delcmd !ping', MOD)).toEqual(['!ping is a built-in command.']);
  });

  it('lists only commands the asker can use', async () => {
    await run('!addcmd !hello Hi', MOD);
    await run('!addcmd -perm=mod !modonly x', MOD);
    await run('!addcmd !off x', MOD);
    updateCommand(db, 'off', {});
    db.prepare("UPDATE commands SET enabled = 0 WHERE name = 'off'").run();

    expect(await run('!commands', VIEWER)).toEqual(['Commands: !commands !hello !ping']);
    expect(await run('!commands', MOD, 'u2')).toEqual([
      'Commands: !addcmd !commands !delcmd !editcmd !hello !modonly !ping',
    ]);
  });
});
