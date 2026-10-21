import type { ApiClient } from '@twurple/api';
import { EventSubWsListener } from '@twurple/eventsub-ws';
import type { ChatMessage } from '../core/chat.js';
import type { Logger } from '../logger.js';

// Twurple opens one WebSocket per authorizing user and creates each
// subscription with that user's token (chat messages: the bot).
export function startEventSub(
  apiClient: ApiClient,
  ids: { broadcasterId: string; botId: string },
  onChat: (msg: ChatMessage) => void,
  log: Logger,
) {
  const listener = new EventSubWsListener({ apiClient });

  listener.onUserSocketConnect((userId) => log.info({ userId }, 'eventsub connected'));
  listener.onUserSocketDisconnect((userId, err) => log.warn({ userId, err }, 'eventsub disconnected'));
  listener.onSubscriptionCreateSuccess((sub) => log.info({ sub: sub.id }, 'subscribed'));
  listener.onSubscriptionCreateFailure((sub, err) => log.error({ sub: sub.id, err }, 'subscription failed'));

  listener.onChannelChatMessage(ids.broadcasterId, ids.botId, (e) => {
    if (e.chatterId === ids.botId) return;
    onChat({
      id: e.messageId,
      text: e.messageText,
      userId: e.chatterId,
      userLogin: e.chatterName,
      userDisplayName: e.chatterDisplayName,
      badges: e.badges,
    });
  });

  listener.start();
  return listener;
}
