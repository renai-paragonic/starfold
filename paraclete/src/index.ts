import { loadConfig } from './config.js';
import { createLogger } from './logger.js';

const config = loadConfig();
const log = createLogger(config.logLevel);

log.info({ channel: config.channel, dbPath: config.dbPath }, 'pneuma starting');

function shutdown(signal: NodeJS.Signals) {
  log.info({ signal }, 'shutting down');
  process.exit(0);
}

process.once('SIGTERM', shutdown);
process.once('SIGINT', shutdown);

// Keep the process alive until later steps add real work (EventSub listener).
setInterval(() => {}, 1 << 30);
