import type { Db } from '../../db/index.js';
import { addCommand, deleteCommand, listCommands, updateCommand } from '../../db/commands.js';
import { getEventResponse, setEventEnabled, setEventTemplate } from '../../db/events.js';
import type { ChatMessage } from '../chat.js';
import { EVENT_TYPES, type EventType } from '../events/handlers.js';
import { allows, parsePermission, type Permission } from './permissions.js';

export type BuiltinContext = { db: Db; msg: ChatMessage; args: string[]; level: Permission };

export type Builtin = {
  permission: Permission;
  cooldownS: number;
  run(ctx: BuiltinContext): string | null;
};

const NAME = /^[a-z0-9_]{1,32}$/;
const OPTIONS_HELP = 'Options: -perm=everyone|sub|vip|mod|broadcaster -cd=seconds';

type ManageArgs = { name?: string; response?: string; permission?: Permission; cooldownS?: number; error?: string };

// [-perm=X] [-cd=N] [!]name [response...]
function parseManageArgs(args: string[]): ManageArgs {
  const out: ManageArgs = {};
  let i = 0;
  for (; i < args.length && args[i]!.startsWith('-'); i++) {
    const [key, value = ''] = args[i]!.slice(1).split('=', 2);
    if (key === 'perm' && parsePermission(value)) out.permission = parsePermission(value);
    else if (key === 'cd' && /^\d+$/.test(value)) out.cooldownS = Number(value);
    else return { error: `Unknown option ${args[i]}. ${OPTIONS_HELP}` };
  }
  const name = args[i]?.replace(/^!/, '').toLowerCase();
  if (name !== undefined && !NAME.test(name)) return { error: 'Command names use letters, numbers and _ only.' };
  out.name = name;
  const response = args.slice(i + 1).join(' ');
  if (response) out.response = response;
  return out;
}

const isEventType = (s: string | undefined): s is EventType => EVENT_TYPES.includes(s as EventType);

function checkName(name: string): string | null {
  return Object.hasOwn(builtins, name) ? `!${name} is a built-in command.` : null;
}

export const builtins: Record<string, Builtin> = {
  ping: { permission: 'everyone', cooldownS: 5, run: () => 'pong' },

  addcmd: {
    permission: 'moderator',
    cooldownS: 0,
    run({ db, msg, args }) {
      const a = parseManageArgs(args);
      if (a.error) return a.error;
      if (!a.name || !a.response) return `Usage: !addcmd [-perm=X] [-cd=N] !name response. ${OPTIONS_HELP}`;
      const reserved = checkName(a.name);
      if (reserved) return reserved;
      const added = addCommand(db, {
        name: a.name,
        response: a.response,
        permission: a.permission ?? 'everyone',
        cooldownS: a.cooldownS ?? 5,
        createdBy: msg.userLogin,
      });
      return added ? `Added !${a.name}.` : `!${a.name} already exists; use !editcmd.`;
    },
  },

  editcmd: {
    permission: 'moderator',
    cooldownS: 0,
    run({ db, args }) {
      const a = parseManageArgs(args);
      if (a.error) return a.error;
      if (!a.name || (a.response === undefined && a.permission === undefined && a.cooldownS === undefined)) {
        return `Usage: !editcmd [-perm=X] [-cd=N] !name [new response]. ${OPTIONS_HELP}`;
      }
      const reserved = checkName(a.name);
      if (reserved) return reserved;
      const updated = updateCommand(db, a.name, a);
      return updated ? `Updated !${a.name}.` : `!${a.name} doesn't exist.`;
    },
  },

  delcmd: {
    permission: 'moderator',
    cooldownS: 0,
    run({ db, args }) {
      const name = args[0]?.replace(/^!/, '').toLowerCase();
      if (!name) return 'Usage: !delcmd !name';
      const reserved = checkName(name);
      if (reserved) return reserved;
      return deleteCommand(db, name) ? `Deleted !${name}.` : `!${name} doesn't exist.`;
    },
  },

  seteventmsg: {
    permission: 'moderator',
    cooldownS: 0,
    run({ db, args }) {
      const type = args[0]?.toLowerCase();
      const template = args.slice(1).join(' ');
      if (!isEventType(type) || !template) return `Usage: !seteventmsg <${EVENT_TYPES.join('|')}> message`;
      setEventTemplate(db, type, template);
      return `Updated the ${type} message.`;
    },
  },

  // !eventmsg type shows the message; !eventmsg type on|off toggles it.
  eventmsg: {
    permission: 'moderator',
    cooldownS: 0,
    run({ db, args }) {
      const type = args[0]?.toLowerCase();
      const toggle = args[1]?.toLowerCase();
      const usage = `Usage: !eventmsg <${EVENT_TYPES.join('|')}> [on|off]`;
      if (!isEventType(type)) return usage;
      if (toggle === undefined) {
        const { template, enabled } = getEventResponse(db, type);
        return `${type} (${enabled ? 'on' : 'off'}): ${template}`;
      }
      if (toggle !== 'on' && toggle !== 'off') return usage;
      setEventEnabled(db, type, toggle === 'on');
      return `Turned the ${type} message ${toggle}.`;
    },
  },

  commands: {
    permission: 'everyone',
    cooldownS: 5,
    run({ db, level }) {
      const names = [
        ...Object.entries(builtins)
          .filter(([, b]) => allows(level, b.permission))
          .map(([name]) => name),
        ...listCommands(db)
          .filter((c) => c.enabled && allows(level, c.permission))
          .map((c) => c.name),
      ].sort();
      return `Commands: ${names.map((n) => `!${n}`).join(' ')}`;
    },
  },
};
