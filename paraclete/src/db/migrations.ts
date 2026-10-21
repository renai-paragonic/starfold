// Ordered, append-only. Index i upgrades the schema from user_version i to i + 1.
// Never edit a migration that has shipped; add a new one instead.
export const migrations: string[] = [
  `
  CREATE TABLE tokens (
    user_id       TEXT PRIMARY KEY,
    role          TEXT NOT NULL UNIQUE CHECK (role IN ('bot', 'broadcaster')),
    access_token  TEXT NOT NULL,
    refresh_token TEXT,
    scopes        TEXT NOT NULL,          -- JSON array
    expires_in    INTEGER,                -- seconds
    obtained_at   INTEGER NOT NULL        -- unix ms
  );

  CREATE TABLE commands (
    name       TEXT PRIMARY KEY,
    response   TEXT NOT NULL,
    permission TEXT NOT NULL DEFAULT 'everyone',
    cooldown_s INTEGER NOT NULL DEFAULT 5,
    enabled    INTEGER NOT NULL DEFAULT 1,
    use_count  INTEGER NOT NULL DEFAULT 0,
    created_by TEXT,
    updated_at INTEGER NOT NULL           -- unix ms
  );

  CREATE TABLE event_responses (
    event_type TEXT PRIMARY KEY,
    template   TEXT NOT NULL,
    enabled    INTEGER NOT NULL DEFAULT 1
  );

  INSERT INTO event_responses (event_type, template) VALUES
    ('follow', 'Thanks for the follow, {user}!'),
    ('sub',    '{user} just subscribed! Welcome aboard!'),
    ('resub',  '{user} resubscribed for {months} months!'),
    ('gift',   '{user} gifted {count} subs!'),
    ('raid',   '{user} is raiding with {viewers} viewers! Welcome, raiders!'),
    ('redeem', '{user} redeemed {reward}!');
  `,
];
