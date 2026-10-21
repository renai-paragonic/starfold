import { z } from 'zod';

const envSchema = z.object({
  TWITCH_CLIENT_ID: z.string().min(1),
  TWITCH_CLIENT_SECRET: z.string().min(1),
  TWITCH_CHANNEL: z.string().min(1).transform((s) => s.toLowerCase()),
  DB_PATH: z.string().min(1).default('./data/pneuma.db'),
  LOG_LEVEL: z.enum(['trace', 'debug', 'info', 'warn', 'error', 'fatal']).default('info'),
});

export type Config = {
  clientId: string;
  clientSecret: string;
  channel: string;
  dbPath: string;
  logLevel: z.infer<typeof envSchema>['LOG_LEVEL'];
};

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  const result = envSchema.safeParse(env);
  if (!result.success) {
    throw new Error(`Invalid environment:\n${z.prettifyError(result.error)}`);
  }
  const e = result.data;
  return {
    clientId: e.TWITCH_CLIENT_ID,
    clientSecret: e.TWITCH_CLIENT_SECRET,
    channel: e.TWITCH_CHANNEL,
    dbPath: e.DB_PATH,
    logLevel: e.LOG_LEVEL,
  };
}
