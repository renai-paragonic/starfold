import { describe, expect, it } from 'vitest';
import { handleChatMessage, type ChatMessage } from '../src/core/chat.js';

const msg = (text: string): ChatMessage => ({
  id: 'm1',
  text,
  userId: '1',
  userLogin: 'viewer',
  userDisplayName: 'Viewer',
  badges: {},
});

describe('handleChatMessage', () => {
  it('answers !ping', () => {
    const replies: string[] = [];
    handleChatMessage(msg(' !PING '), (t) => replies.push(t));
    expect(replies).toEqual(['pong']);
  });

  it('ignores other messages', () => {
    const replies: string[] = [];
    handleChatMessage(msg('hello !ping'), (t) => replies.push(t));
    expect(replies).toEqual([]);
  });
});
