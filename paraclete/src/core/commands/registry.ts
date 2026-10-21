import type { Db } from '../../db/index.js';
import { getCommand, recordUse } from '../../db/commands.js';
import type { ChatMessage, Reply } from '../chat.js';
import { builtins } from './builtins.js';
import { allows, levelOf } from './permissions.js';
import { render, type LiveData } from './templates.js';

export const USER_COOLDOWN_S = 15;
const MAX_MESSAGE_LENGTH = 500; // Twitch's chat limit

export function parseCommand(text: string): { name: string; args: string[] } | null {
  const [first, ...args] = text.trim().split(/\s+/);
  if (!first || !first.startsWith('!') || first.length === 1) return null;
  return { name: first.slice(1).toLowerCase(), args };
}

// In-memory: cooldowns reset when the bot restarts.
export class Cooldowns {
  private readonly lastUsed = new Map<string, number>();

  constructor(private readonly now: () => number = Date.now) {}

  ready(name: string, userId: string, commandS: number, userS: number): boolean {
    const t = this.now();
    const cmd = this.lastUsed.get(name);
    const user = this.lastUsed.get(`${name}:${userId}`);
    return (cmd === undefined || t - cmd >= commandS * 1000) && (user === undefined || t - user >= userS * 1000);
  }

  mark(name: string, userId: string): void {
    const t = this.now();
    this.lastUsed.set(name, t);
    this.lastUsed.set(`${name}:${userId}`, t);
  }
}

export function createCommandHandler(deps: { db: Db; live: LiveData; now?: () => number; random?: () => number }) {
  const cooldowns = new Cooldowns(deps.now);

  return async (msg: ChatMessage, reply: Reply): Promise<void> => {
    const parsed = parseCommand(msg.text);
    if (!parsed) return;
    const { name, args } = parsed;

    const builtin = Object.hasOwn(builtins, name) ? builtins[name] : undefined;
    const custom = builtin ? undefined : getCommand(deps.db, name);
    const command = builtin ?? (custom?.enabled ? custom : undefined);
    if (!command) return;

    const level = levelOf(msg.badges);
    if (!allows(level, command.permission)) return;
    if (!allows(level, 'moderator') && !cooldowns.ready(name, msg.userId, command.cooldownS, USER_COOLDOWN_S)) return;
    cooldowns.mark(name, msg.userId);

    let text: string | null;
    if (builtin) {
      text = builtin.run({ db: deps.db, msg, args, level });
    } else {
      const count = recordUse(deps.db, name);
      const target = args[0]?.replace(/^@/, '');
      text = await render(
        custom!.response,
        {
          user: msg.userDisplayName,
          target: target || msg.userDisplayName,
          targetLogin: target ? target.toLowerCase() : msg.userLogin,
          args,
          channel: msg.channel,
          count,
        },
        deps.live,
        deps.random,
      );
    }

    if (text) reply(text.slice(0, MAX_MESSAGE_LENGTH));
  };
}
