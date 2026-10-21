import type { Permission } from '../core/commands/permissions.js';
import type { Db } from './index.js';

export type Command = {
  name: string;
  response: string;
  permission: Permission;
  cooldownS: number;
  enabled: boolean;
  useCount: number;
  createdBy: string | null;
  updatedAt: number;
};

type Row = {
  name: string;
  response: string;
  permission: Permission;
  cooldown_s: number;
  enabled: number;
  use_count: number;
  created_by: string | null;
  updated_at: number;
};

const fromRow = (r: Row): Command => ({
  name: r.name,
  response: r.response,
  permission: r.permission,
  cooldownS: r.cooldown_s,
  enabled: r.enabled === 1,
  useCount: r.use_count,
  createdBy: r.created_by,
  updatedAt: r.updated_at,
});

export function getCommand(db: Db, name: string): Command | undefined {
  const row = db.prepare('SELECT * FROM commands WHERE name = ?').get(name) as Row | undefined;
  return row && fromRow(row);
}

export function listCommands(db: Db): Command[] {
  return (db.prepare('SELECT * FROM commands ORDER BY name').all() as Row[]).map(fromRow);
}

// Returns false if a command with that name already exists.
export function addCommand(
  db: Db,
  cmd: { name: string; response: string; permission: Permission; cooldownS: number; createdBy: string },
  now = Date.now(),
): boolean {
  const { changes } = db
    .prepare(
      `INSERT INTO commands (name, response, permission, cooldown_s, created_by, updated_at)
       VALUES (?, ?, ?, ?, ?, ?) ON CONFLICT (name) DO NOTHING`,
    )
    .run(cmd.name, cmd.response, cmd.permission, cmd.cooldownS, cmd.createdBy, now);
  return changes === 1;
}

// Returns false if no command has that name.
export function updateCommand(
  db: Db,
  name: string,
  patch: { response?: string; permission?: Permission; cooldownS?: number },
  now = Date.now(),
): boolean {
  const { changes } = db
    .prepare(
      `UPDATE commands SET
         response = COALESCE(:response, response),
         permission = COALESCE(:permission, permission),
         cooldown_s = COALESCE(:cooldown_s, cooldown_s),
         updated_at = :now
       WHERE name = :name`,
    )
    .run({
      name,
      response: patch.response ?? null,
      permission: patch.permission ?? null,
      cooldown_s: patch.cooldownS ?? null,
      now,
    });
  return changes === 1;
}

export function deleteCommand(db: Db, name: string): boolean {
  return db.prepare('DELETE FROM commands WHERE name = ?').run(name).changes === 1;
}

// Increments and returns the new use count.
export function recordUse(db: Db, name: string): number {
  const row = db.prepare('UPDATE commands SET use_count = use_count + 1 WHERE name = ? RETURNING use_count').get(name);
  return Number(row?.use_count ?? 0);
}
