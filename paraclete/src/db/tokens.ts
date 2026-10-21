import type { AccessToken } from '@twurple/auth';
import type { Db } from './index.js';

export type Role = 'bot' | 'broadcaster';

export type StoredToken = { role: Role; userId: string; token: AccessToken };

type Row = {
  user_id: string;
  role: Role;
  access_token: string;
  refresh_token: string | null;
  scopes: string;
  expires_in: number | null;
  obtained_at: number;
};

const tokenParams = (t: AccessToken) => ({
  access_token: t.accessToken,
  refresh_token: t.refreshToken,
  scopes: JSON.stringify(t.scope),
  expires_in: t.expiresIn,
  obtained_at: t.obtainmentTimestamp,
});

// Replaces the token for a role, even if a different account now holds it.
export function saveToken(db: Db, role: Role, userId: string, token: AccessToken): void {
  db.prepare(
    `INSERT INTO tokens (user_id, role, access_token, refresh_token, scopes, expires_in, obtained_at)
     VALUES (:user_id, :role, :access_token, :refresh_token, :scopes, :expires_in, :obtained_at)
     ON CONFLICT (role) DO UPDATE SET
       user_id = excluded.user_id, access_token = excluded.access_token,
       refresh_token = excluded.refresh_token, scopes = excluded.scopes,
       expires_in = excluded.expires_in, obtained_at = excluded.obtained_at`,
  ).run({ user_id: userId, role, ...tokenParams(token) });
}

export function updateToken(db: Db, userId: string, token: AccessToken): void {
  const { changes } = db
    .prepare(
      `UPDATE tokens SET access_token = :access_token, refresh_token = :refresh_token,
         scopes = :scopes, expires_in = :expires_in, obtained_at = :obtained_at
       WHERE user_id = :user_id`,
    )
    .run({ user_id: userId, ...tokenParams(token) });
  if (changes === 0) throw new Error(`No stored token for user ${userId}`);
}

export function loadTokens(db: Db): StoredToken[] {
  const rows = db.prepare('SELECT * FROM tokens ORDER BY role').all() as Row[];
  return rows.map((r) => ({
    role: r.role,
    userId: r.user_id,
    token: {
      accessToken: r.access_token,
      refreshToken: r.refresh_token,
      scope: JSON.parse(r.scopes) as string[],
      expiresIn: r.expires_in,
      obtainmentTimestamp: r.obtained_at,
    },
  }));
}
