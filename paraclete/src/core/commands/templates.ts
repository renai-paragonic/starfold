// Stream data that costs an API call. Implemented in src/twitch/live.ts.
export type LiveData = {
  uptime(): Promise<string>;
  viewers(): Promise<string>;
  game(): Promise<string>;
  title(): Promise<string>;
  followage(login: string): Promise<string>;
  gameOf(login: string): Promise<string>;
};

export type TemplateVars = {
  user: string; // display name of whoever used the command
  target: string; // first arg without '@', else the user
  targetLogin: string;
  args: string[];
  channel: string;
  count: number;
};

const PLACEHOLDER = /\{(\w+)(?::([^{}]*))?\}/g;

// Single pass: text substituted in (e.g. from {args}) is never expanded again.
// Unknown or malformed placeholders are left as written.
export async function render(
  template: string,
  vars: TemplateVars,
  live: LiveData,
  random: () => number = Math.random,
): Promise<string> {
  const matches = [...template.matchAll(PLACEHOLDER)];
  const values = await Promise.all(matches.map(([, name, param]) => valueOf(name!, param, vars, live, random)));
  let i = 0;
  return template.replace(PLACEHOLDER, (whole) => values[i++] ?? whole);
}

async function valueOf(
  name: string,
  param: string | undefined,
  vars: TemplateVars,
  live: LiveData,
  random: () => number,
): Promise<string | undefined> {
  const argN = /^arg(\d+)$/.exec(name);
  if (argN) return vars.args[Number(argN[1]) - 1] ?? '';

  switch (name) {
    case 'user':
      return vars.user;
    case 'target':
      return vars.target;
    case 'args':
      return vars.args.join(' ');
    case 'channel':
      return vars.channel;
    case 'count':
      return String(vars.count);
    case 'random': {
      const range = /^(-?\d+)-(-?\d+)$/.exec(param ?? '');
      if (!range) return undefined;
      const [lo, hi] = [Number(range[1]), Number(range[2])].sort((a, b) => a - b) as [number, number];
      return String(lo + Math.floor(random() * (hi - lo + 1)));
    }
    case 'pick': {
      const options = (param ?? '').split('|').map((s) => s.trim()).filter(Boolean);
      return options.length > 0 ? options[Math.floor(random() * options.length)] : undefined;
    }
    case 'uptime':
      return live.uptime();
    case 'viewers':
      return live.viewers();
    case 'game':
      return live.game();
    case 'title':
      return live.title();
    case 'followage':
      return live.followage(vars.targetLogin);
    case 'targetgame':
      return live.gameOf(vars.targetLogin);
    default:
      return undefined;
  }
}

export function formatUptime(ms: number): string {
  const minutes = Math.floor(ms / 60_000);
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

const DAY_MS = 86_400_000;

function addMonthsUTC(d: Date, n: number): Date {
  const y = d.getUTCFullYear();
  const m = d.getUTCMonth() + n;
  const lastDay = new Date(Date.UTC(y, m + 1, 0)).getUTCDate();
  return new Date(Date.UTC(y, m, Math.min(d.getUTCDate(), lastDay), d.getUTCHours(), d.getUTCMinutes(), d.getUTCSeconds()));
}

const plural = (n: number, unit: string) => `${n} ${unit}${n === 1 ? '' : 's'}`;

// Calendar distance as its two largest units, e.g. "1 year, 3 months".
export function formatAge(from: Date, now: Date): string {
  let months = (now.getUTCFullYear() - from.getUTCFullYear()) * 12 + now.getUTCMonth() - from.getUTCMonth();
  while (months > 0 && addMonthsUTC(from, months) > now) months--;
  const days = Math.floor((now.getTime() - addMonthsUTC(from, months).getTime()) / DAY_MS);

  const parts = [
    [Math.floor(months / 12), 'year'],
    [months % 12, 'month'],
    [days, 'day'],
  ] as const;
  const firstNonZero = parts.findIndex(([n]) => n > 0);
  if (firstNonZero === -1) return 'less than a day';
  return parts
    .slice(firstNonZero, firstNonZero + 2)
    .filter(([n]) => n > 0)
    .map(([n, unit]) => plural(n, unit))
    .join(', ');
}
