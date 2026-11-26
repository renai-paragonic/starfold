import type { ApiClient } from '@twurple/api';
import type { Logger } from '../logger.js';

export type OutgoingMessage = { text: string; replyTo?: string };

type QueueOptions<T> = {
  limit: number;
  windowMs: number;
  onError: (err: unknown, item: T) => void;
};

// Sends items in order, at most `limit` per sliding `windowMs`.
export function createSendQueue<T>(send: (item: T) => Promise<void>, opts: QueueOptions<T>) {
  const queue: T[] = [];
  const sentAt: number[] = [];
  let draining = false;

  async function drain() {
    draining = true;
    while (queue.length > 0) {
      const now = Date.now();
      while (sentAt.length > 0 && sentAt[0]! <= now - opts.windowMs) sentAt.shift();
      if (sentAt.length >= opts.limit) {
        await new Promise((r) => setTimeout(r, sentAt[0]! + opts.windowMs - now));
        continue;
      }
      const item = queue.shift()!;
      sentAt.push(now);
      try {
        await send(item);
      } catch (err) {
        opts.onError(err, item);
      }
    }
    draining = false;
  }

  return {
    push(item: T) {
      queue.push(item);
      if (!draining) void drain();
    },
  };
}

// Chat sender that posts as the bot account through Helix.
export function createChatSender(
  apiClient: ApiClient,
  ids: { broadcasterId: string; botId: string },
  limit: number,
  log: Logger,
) {
  return createSendQueue<OutgoingMessage>(
    async ({ text, replyTo }) => {
      const result = await apiClient.asUser(ids.botId, (ctx) =>
        ctx.chat.sendChatMessage(ids.broadcasterId, text, { replyParentMessageId: replyTo }),
      );
      if (!result.isSent) {
        log.warn({ text, code: result.dropReasonCode, reason: result.dropReasonMessage }, 'message dropped by Twitch');
      }
    },
    {
      limit,
      windowMs: 30_000,
      onError: (err, item) => log.error({ err, text: item.text }, 'failed to send chat message'),
    },
  );
}
