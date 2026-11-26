import type { Db } from '../../db/index.js';
import { getEventResponse } from '../../db/events.js';
import { MAX_MESSAGE_LENGTH } from '../chat.js';
import { render, type LiveData } from '../commands/templates.js';

type EventUser = { login: string; displayName: string };

// Transport-agnostic stream events. Fields other than type, user, and channel
// become template placeholders, e.g. {months} or {viewers}.
export type BotEvent = { channel: string } & (
  | { type: 'follow'; user: EventUser }
  | { type: 'sub'; user: EventUser }
  | { type: 'resub'; user: EventUser; months: number }
  | { type: 'gift'; user: EventUser | null; count: number } // null: anonymous gifter
  | { type: 'raid'; user: EventUser; viewers: number }
  | { type: 'redeem'; user: EventUser; reward: string }
);

export type EventType = BotEvent['type'];

export const EVENT_TYPES: readonly EventType[] = ['follow', 'sub', 'resub', 'gift', 'raid', 'redeem'];

const ANONYMOUS: EventUser = { login: '', displayName: 'An anonymous gifter' };

export function createEventHandler(deps: { db: Db; live: LiveData }) {
  return async (event: BotEvent, say: (text: string) => void): Promise<void> => {
    const response = getEventResponse(deps.db, event.type);
    if (!response.enabled) return;

    const { type, user, channel, ...fields } = event;
    const who = user ?? ANONYMOUS;
    const text = await render(
      response.template,
      {
        user: who.displayName,
        target: who.displayName,
        targetLogin: who.login,
        args: [],
        channel,
        count: 0,
        extra: Object.fromEntries(Object.entries(fields).map(([k, v]) => [k, String(v)])),
      },
      deps.live,
    );
    if (text) say(text.slice(0, MAX_MESSAGE_LENGTH));
  };
}
