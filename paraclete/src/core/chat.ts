// Transport-agnostic chat types. Twitch specifics stay in src/twitch/.
export type ChatMessage = {
  id: string;
  text: string;
  userId: string;
  userLogin: string;
  userDisplayName: string;
  badges: Record<string, string>;
};

export type Reply = (text: string) => void;

// Placeholder until the command registry lands in step 5.
export function handleChatMessage(msg: ChatMessage, reply: Reply): void {
  if (msg.text.trim().toLowerCase() === '!ping') reply('pong');
}
