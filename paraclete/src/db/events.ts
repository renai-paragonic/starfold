import type { EventType } from '../core/events/handlers.js';
import type { Db } from './index.js';

export type EventResponse = { template: string; enabled: boolean };

// Every event type has a row, seeded by the first migration.
export function getEventResponse(db: Db, type: EventType): EventResponse {
  const row = db.prepare('SELECT template, enabled FROM event_responses WHERE event_type = ?').get(type) as {
    template: string;
    enabled: number;
  };
  return { template: row.template, enabled: row.enabled === 1 };
}

export function setEventTemplate(db: Db, type: EventType, template: string): void {
  db.prepare('UPDATE event_responses SET template = ? WHERE event_type = ?').run(template, type);
}

export function setEventEnabled(db: Db, type: EventType, enabled: boolean): void {
  db.prepare('UPDATE event_responses SET enabled = ? WHERE event_type = ?').run(enabled ? 1 : 0, type);
}
