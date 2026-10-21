import { ApiClient } from '@twurple/api';
import { loadConfig } from './config.js';
import { handleChatMessage } from './core/chat.js';
import { openDb } from './db/index.js';
import { createLogger } from './logger.js';
import { createAuthProvider } from './twitch/auth.js';
import { startEventSub } from './twitch/eventsub.js';
import { createChatSender } from './twitch/sender.js';

const config = loadConfig();
const log = createLogger(config.logLevel);

log.info({ channel: config.channel, dbPath: config.dbPath }, 'paraclete starting');

const db = openDb(config.dbPath);
log.info('database ready');

const { provider, userIds } = createAuthProvider(db, config, log);
const { bot: botId, broadcaster: broadcasterId } = userIds;
if (!botId || !broadcasterId) {
  for (const role of ['bot', 'broadcaster'] as const) {
    if (!userIds[role]) log.fatal(`no ${role} token; run: npm run auth:setup -- --role ${role}`);
  }
  process.exit(1);
}

const apiClient = new ApiClient({ authProvider: provider });
const ids = { broadcasterId, botId };
const sender = createChatSender(apiClient, ids, config.chatRateLimit, log);
const listener = startEventSub(
  apiClient,
  ids,
  (msg) => handleChatMessage(msg, (text) => sender.push({ text, replyTo: msg.id })),
  log,
);

function shutdown(signal: NodeJS.Signals) {
  log.info({ signal }, 'shutting down');
  listener.stop();
  db.close();
  process.exit(0);
}

process.once('SIGTERM', shutdown);
process.once('SIGINT', shutdown);
