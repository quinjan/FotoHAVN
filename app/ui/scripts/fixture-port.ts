export const defaultFixturePort = 4173;

// The fixture lane's preview server port. Parallel worktrees set FOTOHAVN_FIXTURE_PORT so their lanes don't collide.
export function fixturePort(env: NodeJS.ProcessEnv = process.env): number {
  const raw = env.FOTOHAVN_FIXTURE_PORT;
  if (raw === undefined || raw.trim() === "") {
    return defaultFixturePort;
  }
  const port = Number(raw);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`FOTOHAVN_FIXTURE_PORT must be a port number from 1 to 65535, got "${raw}".`);
  }
  return port;
}
