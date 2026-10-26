import { beforeEach, describe, expect, it } from 'vitest';
import type { ChatMessage } from '../src/core/chat.js';
import { createCommandHandler } from '../src/core/commands/registry.js';
import type { LiveData } from '../src/core/commands/templates.js';
import { createEventHandler, type BotEvent } from '../src/core/events/handlers.js';
import { openDb, type Db } from '../src/db/index.js';

const live: LiveData = {
  uptime: async () => '1h',
  viewers: async () => '1',
  game: async () => 'Game',
  title: async () => 'Title',
  followage: async () => '1 year',
  gameOf: async (login) => `${login}'s game`,
};

const alice = { login: 'alice', displayName: 'Alice' };
const channel = 'Streamer';

let db: Db;
let run: (event: BotEvent) => Promise<string[]>;

beforeEach(() => {
  db = openDb(':memory:');
  const handle = createEventHandler({ db, live });
  run = async (event) => {
    const said: string[] = [];
    await handle(event, (t) => said.push(t));
    return said;
  };
});

const setTemplate = (type: string, template: string) =>
  db.prepare('UPDATE event_responses SET template = ? WHERE event_type = ?').run(template, type);

describe('event handler with seeded templates', () => {
  it('follow', async () => {
    expect(await run({ type: 'follow', user: alice, channel })).toEqual(['Thanks for the follow, Alice!']);
  });
  it('sub', async () => {
    expect(await run({ type: 'sub', user: alice, channel })).toEqual(['Alice just subscribed! Welcome aboard!']);
  });
  it('resub', async () => {
    expect(await run({ type: 'resub', user: alice, months: 7, channel })).toEqual(['Alice resubscribed for 7 months!']);
  });
  it('gift, with {count} as the gift amount', async () => {
    expect(await run({ type: 'gift', user: alice, count: 5, channel })).toEqual(['Alice gifted 5 subs!']);
  });
  it('anonymous gift', async () => {
    expect(await run({ type: 'gift', user: null, count: 1, channel })).toEqual(['An anonymous gifter gifted 1 subs!']);
  });
  it('raid, with {viewers} as the raid size rather than the live count', async () => {
    expect(await run({ type: 'raid', user: alice, viewers: 42, channel })).toEqual([
      'Alice is raiding with 42 viewers! Welcome, raiders!',
    ]);
  });
  it('redeem', async () => {
    expect(await run({ type: 'redeem', user: alice, reward: 'Hydrate', channel })).toEqual(['Alice redeemed Hydrate!']);
  });
});

describe('event handler', () => {
  it('stays quiet when the response is disabled', async () => {
    db.prepare("UPDATE event_responses SET enabled = 0 WHERE event_type = 'follow'").run();
    expect(await run({ type: 'follow', user: alice, channel })).toEqual([]);
  });
  it('resolves per-user placeholders against the event user', async () => {
    setTemplate('raid', 'Go follow {user}, last seen playing {targetgame} in {channel}');
    expect(await run({ type: 'raid', user: alice, viewers: 3, channel })).toEqual([
      "Go follow Alice, last seen playing alice's game in Streamer",
    ]);
  });
  it('truncates to the chat limit', async () => {
    setTemplate('redeem', '{reward}');
    const [text] = await run({ type: 'redeem', user: alice, reward: 'x'.repeat(600), channel });
    expect(text).toHaveLength(500);
  });
});

describe('event message commands', () => {
  const MOD = { moderator: '1' };
  let chat: (text: string, badges?: Record<string, string>) => Promise<string[]>;

  beforeEach(() => {
    const handle = createCommandHandler({ db, live });
    chat = async (text, badges = MOD) => {
      const msg: ChatMessage = {
        id: 'm',
        text,
        userId: 'u1',
        userLogin: 'mod',
        userDisplayName: 'Mod',
        badges,
        channel,
      };
      const replies: string[] = [];
      await handle(msg, (t) => replies.push(t));
      return replies;
    };
  });

  it('sets a message, case-insensitively', async () => {
    expect(await chat('!seteventmsg Follow Welcome in, {user}!')).toEqual(['Updated the follow message.']);
    expect(await run({ type: 'follow', user: alice, channel })).toEqual(['Welcome in, Alice!']);
    expect(db.prepare('SELECT COUNT(*) AS n FROM event_responses').get()?.n).toBe(6);
  });

  it('shows and toggles a message', async () => {
    expect(await chat('!eventmsg follow')).toEqual(['follow (on): Thanks for the follow, {user}!']);
    expect(await chat('!eventmsg follow OFF')).toEqual(['Turned the follow message off.']);
    expect(await run({ type: 'follow', user: alice, channel })).toEqual([]);
    expect(await chat('!eventmsg follow')).toEqual(['follow (off): Thanks for the follow, {user}!']);
    expect(await chat('!eventmsg follow on')).toEqual(['Turned the follow message on.']);
    expect(await run({ type: 'follow', user: alice, channel })).toHaveLength(1);
  });

  it('rejects bad input', async () => {
    const setUsage = 'Usage: !seteventmsg <follow|sub|resub|gift|raid|redeem> message';
    const toggleUsage = 'Usage: !eventmsg <follow|sub|resub|gift|raid|redeem> [on|off]';
    expect(await chat('!seteventmsg')).toEqual([setUsage]);
    expect(await chat('!seteventmsg follow')).toEqual([setUsage]);
    expect(await chat('!seteventmsg cheer Thanks')).toEqual([setUsage]);
    expect(await chat('!eventmsg cheer')).toEqual([toggleUsage]);
    expect(await chat('!eventmsg follow maybe')).toEqual([toggleUsage]);
  });

  it('is mod-only', async () => {
    expect(await chat('!seteventmsg follow hijacked', {})).toEqual([]);
    expect(await chat('!eventmsg follow off', {})).toEqual([]);
    expect(await run({ type: 'follow', user: alice, channel })).toEqual(['Thanks for the follow, Alice!']);
  });
});
