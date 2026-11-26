import { pino } from 'pino';

export function createLogger(level: string) {
  return pino({ name: 'paraclete', level });
}

export type Logger = ReturnType<typeof createLogger>;
