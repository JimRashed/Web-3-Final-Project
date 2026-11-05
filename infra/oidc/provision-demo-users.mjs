// Creates or resets the local demo players in the quizarena realm.
// Usage (repo root): node --env-file=.env infra/oidc/provision-demo-users.mjs
// Development only. Passwords come from the environment and are never printed.

const base = process.env.KEYCLOAK_URL ?? "http://localhost:8080";
const realm = "quizarena";
const required = ["KC_BOOTSTRAP_ADMIN_USERNAME", "KC_BOOTSTRAP_ADMIN_PASSWORD", "QUIZARENA_DEMO_PASSWORD"];
const missing = required.filter((name) => !process.env[name]);
if (missing.length) {
  console.error(`Missing environment variables: ${missing.join(", ")}`);
  process.exit(1);
}

const players = [
  { username: "player-a", firstName: "Player", lastName: "A", email: "player-a@quizarena.invalid" },
  { username: "player-b", firstName: "Player", lastName: "B", email: "player-b@quizarena.invalid" },
];

async function adminToken() {
  const response = await fetch(`${base}/realms/master/protocol/openid-connect/token`, {
    method: "POST",
    body: new URLSearchParams({
      grant_type: "password",
      client_id: "admin-cli",
      username: process.env.KC_BOOTSTRAP_ADMIN_USERNAME,
      password: process.env.KC_BOOTSTRAP_ADMIN_PASSWORD,
    }),
  });
  if (!response.ok) throw new Error(`Admin sign-in failed: HTTP ${response.status}`);
  return (await response.json()).access_token;
}

async function admin(token, method, path, body) {
  const response = await fetch(`${base}/admin/realms/${realm}${path}`, {
    method,
    headers: { authorization: `Bearer ${token}`, "content-type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok && response.status !== 409) throw new Error(`${method} ${path}: HTTP ${response.status}`);
  return response;
}

const token = await adminToken();
for (const player of players) {
  const created = await admin(token, "POST", "/users", { ...player, enabled: true, emailVerified: true, requiredActions: [] });
  const found = await (await admin(token, "GET", `/users?username=${encodeURIComponent(player.username)}&exact=true`)).json();
  if (found.length !== 1) throw new Error(`Could not resolve ${player.username}`);
  await admin(token, "PUT", `/users/${found[0].id}/reset-password`, {
    type: "password",
    value: process.env.QUIZARENA_DEMO_PASSWORD,
    temporary: false,
  });
  console.log(`${player.username}: ${created.status === 409 ? "existed, password reset" : "created"}`);
}
