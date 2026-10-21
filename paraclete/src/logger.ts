import { pino } from 'pino';

export function createLogger(level: string) {
  return pino({ name: 'pneuma', level });
}

export type Logger = ReturnType<typeof createLogger>;
