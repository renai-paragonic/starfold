import { RefreshingAuthProvider } from '@twurple/auth';
import type { Config } from '../config.js';
import type { Db } from '../db/index.js';
import { loadTokens, updateToken, type Role } from '../db/tokens.js';
import type { Logger } from '../logger.js';

export const ROLE_SCOPES: Record<Role, string[]> = {
  bot: ['user:read:chat', 'user:write:chat', 'user:bot'],
  broadcaster: [
    'channel:bot',
    'channel:read:subscriptions',
    'channel:read:redemptions',
    'moderator:read:followers',
  ],
};

// Each stored user is registered under an intent named after its role,
// so later code can ask for the 'bot' or 'broadcaster' token directly.
export function createAuthProvider(db: Db, config: Config, log: Logger) {
  const provider = new RefreshingAuthProvider({
    clientId: config.clientId,
    clientSecret: config.clientSecret,
  });

  provider.onRefresh((userId, token) => {
    updateToken(db, userId, token);
    log.debug({ userId }, 'token refreshed');
  });
  provider.onRefreshFailure((userId, err) => {
    log.error({ userId, err }, 'token refresh failed; re-run auth:setup for this account');
  });

  const roles: Role[] = [];
  for (const { role, userId, token } of loadTokens(db)) {
    provider.addUser(userId, token, [role]);
    roles.push(role);
  }

  return { provider, roles };
}
