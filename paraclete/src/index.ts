import { loadConfig } from './config.js';
import { openDb } from './db/index.js';
import { createLogger } from './logger.js';

const config = loadConfig();
const log = createLogger(config.logLevel);

log.info({ channel: config.channel, dbPath: config.dbPath }, 'pneuma starting');

const db = openDb(config.dbPath);
log.info('database ready');

function shutdown(signal: NodeJS.Signals) {
  log.info({ signal }, 'shutting down');
  db.close();
  process.exit(0);
}

process.once('SIGTERM', shutdown);
process.once('SIGINT', shutdown);

// Keep the process alive until later steps add real work (EventSub listener).
setInterval(() => {}, 1 << 30);
