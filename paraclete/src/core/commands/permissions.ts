export const LEVELS = ['everyone', 'subscriber', 'vip', 'moderator', 'broadcaster'] as const;
export type Permission = (typeof LEVELS)[number];

export const rank = (p: Permission) => LEVELS.indexOf(p);

export const allows = (level: Permission, required: Permission) => rank(level) >= rank(required);

// Highest level a chatter's badges grant. Each level includes the ones below it.
export function levelOf(badges: Record<string, string>): Permission {
  if ('broadcaster' in badges) return 'broadcaster';
  if ('moderator' in badges || 'lead_moderator' in badges) return 'moderator';
  if ('vip' in badges) return 'vip';
  if ('subscriber' in badges || 'founder' in badges) return 'subscriber';
  return 'everyone';
}

const ALIASES: Record<string, Permission> = {
  everyone: 'everyone',
  all: 'everyone',
  sub: 'subscriber',
  subs: 'subscriber',
  subscriber: 'subscriber',
  vip: 'vip',
  mod: 'moderator',
  mods: 'moderator',
  moderator: 'moderator',
  broadcaster: 'broadcaster',
};

export function parsePermission(s: string): Permission | undefined {
  return Object.hasOwn(ALIASES, s.toLowerCase()) ? ALIASES[s.toLowerCase()] : undefined;
}
