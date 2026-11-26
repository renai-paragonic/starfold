// Transport-agnostic chat types. Twitch specifics stay in src/twitch/.
export type ChatMessage = {
  id: string;
  text: string;
  userId: string;
  userLogin: string;
  userDisplayName: string;
  badges: Record<string, string>;
  channel: string; // broadcaster display name
};

export type Reply = (text: string) => void;

export const MAX_MESSAGE_LENGTH = 500; // Twitch's chat limit
