import type { ApiClient } from '@twurple/api';
import { formatAge, formatUptime, type LiveData } from '../core/commands/templates.js';

const CACHE_MS = 30_000;

// Caches a fetch for CACHE_MS; concurrent callers share one in-flight request.
function cached<T>(fetch: () => Promise<T>): () => Promise<T> {
  let value: Promise<T> | undefined;
  let fetchedAt = 0;
  return () => {
    if (!value || Date.now() - fetchedAt > CACHE_MS) {
      fetchedAt = Date.now();
      value = fetch().catch((err: unknown) => {
        value = undefined;
        throw err;
      });
    }
    return value;
  };
}

export function createLiveData(apiClient: ApiClient, broadcasterId: string): LiveData {
  const stream = cached(() => apiClient.streams.getStreamByUserId(broadcasterId));
  const channel = cached(() => apiClient.channels.getChannelInfoById(broadcasterId));

  return {
    async uptime() {
      const s = await stream();
      return s ? formatUptime(Date.now() - s.startDate.getTime()) : 'offline';
    },
    async viewers() {
      const s = await stream();
      return s ? String(s.viewers) : 'offline';
    },
    async game() {
      return (await channel())?.gameName || 'no category';
    },
    async title() {
      return (await channel())?.title ?? '';
    },
    // Runs with the broadcaster's token, which holds moderator:read:followers.
    async followage(login) {
      const user = await apiClient.users.getUserByName(login);
      if (!user) return 'not following';
      const { data } = await apiClient.channels.getChannelFollowers(broadcasterId, user.id);
      const followed = data[0]?.followDate;
      return followed ? formatAge(followed, new Date()) : 'not following';
    },
    // Twitch keeps a channel's last category while it's offline.
    async gameOf(login) {
      const user = await apiClient.users.getUserByName(login);
      if (!user) return 'unknown';
      return (await apiClient.channels.getChannelInfoById(user.id))?.gameName || 'nothing yet';
    },
  };
}
