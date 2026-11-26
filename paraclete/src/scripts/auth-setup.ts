// One-time login for the bot or broadcaster account via Twitch's Device Code flow.
// Usage: npm run auth:setup -- --role bot|broadcaster
import { parseArgs } from 'node:util';
import { setTimeout as sleep } from 'node:timers/promises';
import { HttpStatusCodeError } from '@twurple/api-call';
import { exchangeDeviceCode, getTokenInfo, startDeviceCodeFlow, type AccessToken } from '@twurple/auth';
import { loadConfig } from '../config.js';
import { openDb } from '../db/index.js';
import { saveToken, type Role } from '../db/tokens.js';
import { ROLE_SCOPES } from '../twitch/auth.js';

const { values } = parseArgs({ options: { role: { type: 'string' } } });
const role = values.role;
if (role !== 'bot' && role !== 'broadcaster') {
  console.error('Usage: npm run auth:setup -- --role bot|broadcaster');
  process.exit(1);
}

const config = loadConfig();
const scopes = ROLE_SCOPES[role];
const flow = await startDeviceCodeFlow(config.clientId, scopes);

console.log(`\nLog in to Twitch as the ${role.toUpperCase()} account, then open:\n`);
console.log(`  ${flow.verificationUri}\n`);
console.log(`and confirm the code ${flow.userCode}. Waiting for approval...\n`);

const token = await waitForApproval(flow.deviceCode, flow.interval, flow.expiresIn);
const info = await getTokenInfo(token.accessToken, config.clientId);
if (!info.userId || !info.userName) throw new Error('Token has no user attached');

if (role === 'broadcaster' && info.userName !== config.channel) {
  console.error(`Authorized as "${info.userName}", but TWITCH_CHANNEL is "${config.channel}". Nothing saved.`);
  process.exit(1);
}

const db = openDb(config.dbPath);
saveToken(db, role as Role, info.userId, token);
db.close();
console.log(`Saved ${role} token for ${info.userName} (${info.userId}).`);

async function waitForApproval(deviceCode: string, interval: number, expiresIn: number): Promise<AccessToken> {
  const deadline = Date.now() + expiresIn * 1000;
  while (Date.now() < deadline) {
    await sleep(interval * 1000);
    try {
      return await exchangeDeviceCode(config.clientId, deviceCode, scopes);
    } catch (err) {
      if (!(err instanceof HttpStatusCodeError)) throw err;
      if (err.body.includes('authorization_pending')) continue;
      if (err.body.includes('slow_down')) {
        interval += 5;
        continue;
      }
      throw err;
    }
  }
  throw new Error('Device code expired before it was approved; run the command again.');
}
