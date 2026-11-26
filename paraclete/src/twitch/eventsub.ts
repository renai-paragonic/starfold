import type { ApiClient } from '@twurple/api';
import { EventSubWsListener } from '@twurple/eventsub-ws';
import type { ChatMessage } from '../core/chat.js';
import type { BotEvent } from '../core/events/handlers.js';
import type { Logger } from '../logger.js';

// Twurple opens one WebSocket per authorizing user and creates each
// subscription with that user's token (chat messages: the bot; stream events:
// the broadcaster).
export function startEventSub(
  apiClient: ApiClient,
  ids: { broadcasterId: string; botId: string },
  onChat: (msg: ChatMessage) => void,
  onEvent: (event: BotEvent) => void,
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
      channel: e.broadcasterDisplayName,
    });
  });

  const b = ids.broadcasterId;
  const user = (e: { userName: string; userDisplayName: string }) => ({ login: e.userName, displayName: e.userDisplayName });
  const emit = (event: BotEvent) => {
    log.info({ event }, 'stream event');
    onEvent(event);
  };

  // The broadcaster's token holds moderator:read:followers, so they act as the moderator.
  listener.onChannelFollow(b, b, (e) => emit({ type: 'follow', user: user(e), channel: e.broadcasterDisplayName }));
  // Gifted subs also fire this per recipient; the gift event covers them.
  listener.onChannelSubscription(b, (e) => {
    if (!e.isGift) emit({ type: 'sub', user: user(e), channel: e.broadcasterDisplayName });
  });
  // Twitch only sends resubs when the viewer shares the resub message in chat.
  listener.onChannelSubscriptionMessage(b, (e) =>
    emit({ type: 'resub', user: user(e), months: e.cumulativeMonths, channel: e.broadcasterDisplayName }),
  );
  listener.onChannelSubscriptionGift(b, (e) =>
    emit({
      type: 'gift',
      user: e.isAnonymous || !e.gifterName ? null : { login: e.gifterName, displayName: e.gifterDisplayName! },
      count: e.amount,
      channel: e.broadcasterDisplayName,
    }),
  );
  listener.onChannelRaidTo(b, (e) =>
    emit({
      type: 'raid',
      user: { login: e.raidingBroadcasterName, displayName: e.raidingBroadcasterDisplayName },
      viewers: e.viewers,
      channel: e.raidedBroadcasterDisplayName,
    }),
  );
  listener.onChannelRedemptionAdd(b, (e) =>
    emit({ type: 'redeem', user: user(e), reward: e.rewardTitle, channel: e.broadcasterDisplayName }),
  );

  listener.start();
  return listener;
}
