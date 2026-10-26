import { ApiClient } from '@twurple/api';
import { loadConfig } from './config.js';
import { createCommandHandler } from './core/commands/registry.js';
import { createEventHandler } from './core/events/handlers.js';
import { openDb } from './db/index.js';
import { createLogger } from './logger.js';
import { createAuthProvider } from './twitch/auth.js';
import { startEventSub } from './twitch/eventsub.js';
import { createLiveData } from './twitch/live.js';
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
const live = createLiveData(apiClient, broadcasterId);
const handleCommand = createCommandHandler({ db, live });
const handleEvent = createEventHandler({ db, live });
const listener = startEventSub(
  apiClient,
  ids,
  (msg) => {
    handleCommand(msg, (text) => sender.push({ text, replyTo: msg.id })).catch((err: unknown) =>
      log.error({ err, text: msg.text }, 'command failed'),
    );
  },
  (event) => {
    handleEvent(event, (text) => sender.push({ text })).catch((err: unknown) =>
      log.error({ err, event }, 'event response failed'),
    );
  },
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
