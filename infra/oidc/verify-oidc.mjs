// Verifies the local Keycloak realm against issue #20 (P05) and docs/milestone3/authorization-matrix.md.
// Usage (repo root, Keycloak running, demo users provisioned):
//   node --env-file=.env infra/oidc/verify-oidc.mjs
// Prints PASS/FAIL per check and exits non-zero on any failure. Tokens and secrets are never printed.

import { createHash, createPublicKey, createVerify, randomBytes } from "node:crypto";

const authority = process.env.OIDC_AUTHORITY ?? "http://localhost:8080/realms/quizarena";
const redirectUri = "http://localhost:5173/auth/callback";
const gameplaySecret = process.env.QUIZARENA_GAMEPLAY_CLIENT_SECRET;
const demoPassword = process.env.QUIZARENA_DEMO_PASSWORD;
if (!gameplaySecret || !demoPassword) {
  console.error("Set QUIZARENA_GAMEPLAY_CLIENT_SECRET and QUIZARENA_DEMO_PASSWORD (node --env-file=.env ...).");
  process.exit(1);
}

const USER_SCOPES = {
  "profiles.read": "quizarena-identity", "profiles.write": "quizarena-identity",
  "stats.read": "quizarena-statistics", "leaderboard.read": "quizarena-statistics",
  "questions.read": "quizarena-questionbank",
  "lobbies.read": "quizarena-lobby", "lobbies.write": "quizarena-lobby",
  "matches.read": "quizarena-gameplay", "matches.write": "quizarena-gameplay",
  "events.read": "quizarena-notification", "preferences.read": "quizarena-notification", "preferences.write": "quizarena-notification",
};
const MACHINE_SCOPES = {
  "lobbies.verify": "quizarena-lobby", "questions.play": "quizarena-questionbank",
  "results.write": "quizarena-statistics", "events.write": "quizarena-notification",
};

const results = [];
function check(name, ok, detail = "") {
  results.push({ name, ok });
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}${detail ? ` — ${detail}` : ""}`);
}

const b64url = (buffer) => buffer.toString("base64url");
const decode = (jwt) => JSON.parse(Buffer.from(jwt.split(".")[1], "base64url").toString("utf8"));
const audiences = (claims) => (Array.isArray(claims.aud) ? claims.aud : claims.aud ? [claims.aud] : []);
const scopes = (claims) => (claims.scope ?? "").split(" ").filter(Boolean);

async function token(params) {
  const response = await fetch(discovery.token_endpoint, { method: "POST", body: new URLSearchParams(params) });
  return { status: response.status, body: await response.json() };
}

function signatureValid(jwt) {
  const [header, payload, signature] = jwt.split(".");
  const { kid, alg } = JSON.parse(Buffer.from(header, "base64url").toString("utf8"));
  const jwk = jwks.keys.find((key) => key.kid === kid);
  if (!jwk || alg !== "RS256") return false;
  return createVerify("RSA-SHA256").update(`${header}.${payload}`).verify(createPublicKey({ key: jwk, format: "jwk" }), Buffer.from(signature, "base64url"));
}

// --- Discovery and keys ---
const discovery = await (await fetch(`${authority}/.well-known/openid-configuration`)).json();
check("Discovery issuer is the pinned authority", discovery.issuer === authority, discovery.issuer);
const jwks = await (await fetch(discovery.jwks_uri)).json();
check("JWKS publishes an RS256 signing key", jwks.keys.some((key) => key.use === "sig" && key.alg === "RS256"), discovery.jwks_uri);

// --- Gameplay machine client ---
for (const [scope, aud] of Object.entries(MACHINE_SCOPES)) {
  const { status, body } = await token({ grant_type: "client_credentials", client_id: "quizarena-gameplay", client_secret: gameplaySecret, scope });
  if (status !== 200) { check(`Machine token for ${scope}`, false, `HTTP ${status} ${body.error}`); continue; }
  const claims = decode(body.access_token);
  check(`Machine token for ${scope} has only audience ${aud}`,
    audiences(claims).length === 1 && audiences(claims)[0] === aud && scopes(claims).includes(scope) &&
    claims.azp === "quizarena-gameplay" && claims.qa_actor === "machine" && Boolean(claims.sub) && signatureValid(body.access_token),
    `aud=${audiences(claims).join(",")} scope=${claims.scope}`);
}
{
  const { status, body } = await token({ grant_type: "client_credentials", client_id: "quizarena-gameplay", client_secret: gameplaySecret, scope: "stats.read profiles.read" });
  const claims = body.access_token ? decode(body.access_token) : {};
  check("Machine client cannot obtain user scopes", !scopes(claims).some((s) => s in USER_SCOPES) && audiences(claims).length === 0,
    body.access_token ? `scope=${claims.scope}` : `HTTP ${status} ${body.error}`);
}
{
  const { status, body } = await token({ grant_type: "client_credentials", client_id: "quizarena-gameplay", client_secret: "wrong-secret" });
  check("Machine client rejects a wrong secret", status === 401 && ["unauthorized_client", "invalid_client"].includes(body.error), `HTTP ${status} ${body.error}`);
}

// --- Browser public client: forbidden grants ---
{
  const { status, body } = await token({ grant_type: "password", client_id: "quizarena-client", username: "player-a", password: demoPassword });
  check("Browser client: password grant disabled", status >= 400 && !body.access_token, `HTTP ${status} ${body.error}`);
}
{
  const { status, body } = await token({ grant_type: "client_credentials", client_id: "quizarena-client" });
  check("Browser client: client credentials refused", status >= 400 && !body.access_token, `HTTP ${status} ${body.error}`);
}

function authorizeUrl(extra) {
  const url = new URL(discovery.authorization_endpoint);
  for (const [key, value] of Object.entries({ client_id: "quizarena-client", redirect_uri: redirectUri, state: b64url(randomBytes(16)), nonce: b64url(randomBytes(16)), ...extra })) {
    if (value !== undefined) url.searchParams.set(key, value);
  }
  return url;
}

async function rejectedAtAuthorize(name, extra) {
  const response = await fetch(authorizeUrl(extra), { redirect: "manual" });
  const location = response.headers.get("location") ?? "";
  const error = location ? new URL(location).searchParams.get("error") ?? new URLSearchParams(new URL(location).hash.slice(1)).get("error") : null;
  check(name, response.status === 400 || Boolean(error), error ? `error=${error}` : `HTTP ${response.status}`);
}
await rejectedAtAuthorize("Browser client: implicit flow (response_type=token) refused", { response_type: "token", scope: "openid" });
await rejectedAtAuthorize("Browser client: code flow without PKCE refused", { response_type: "code", scope: "openid" });
await rejectedAtAuthorize("Browser client: plain PKCE method refused", { response_type: "code", scope: "openid", code_challenge: "x".repeat(43), code_challenge_method: "plain" });
await rejectedAtAuthorize("Browser client: requesting a machine scope is refused", { response_type: "code", scope: "openid stats.read results.write", code_challenge: "x".repeat(43), code_challenge_method: "S256" });
await rejectedAtAuthorize("Browser client: unregistered redirect URI refused", { response_type: "code", scope: "openid", redirect_uri: "http://evil.invalid/callback", code_challenge: "x".repeat(43), code_challenge_method: "S256" });

// --- Browser public client: full authorization code + PKCE S256 sign-in ---
async function pkceSignIn(requestedScope) {
  const verifier = b64url(randomBytes(32));
  const challenge = b64url(createHash("sha256").update(verifier).digest());
  const cookies = new Map();
  const remember = (response) => {
    for (const cookie of response.headers.getSetCookie()) {
      const [pair] = cookie.split(";");
      const index = pair.indexOf("=");
      cookies.set(pair.slice(0, index), pair.slice(index + 1));
    }
  };
  const cookieHeader = () => [...cookies].map(([k, v]) => `${k}=${v}`).join("; ");

  const loginPage = await fetch(authorizeUrl({ response_type: "code", scope: requestedScope, code_challenge: challenge, code_challenge_method: "S256" }), { redirect: "manual" });
  remember(loginPage);
  const html = await loginPage.text();
  const action = html.match(/<form[^>]*id="kc-form-login"[^>]*action="([^"]+)"/)?.[1]?.replaceAll("&amp;", "&");
  if (!action) throw new Error("Login form not found");
  const submitted = await fetch(action, {
    method: "POST", redirect: "manual",
    headers: { cookie: cookieHeader(), "content-type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ username: "player-a", password: demoPassword, credentialId: "" }),
  });
  const location = submitted.headers.get("location");
  if (!location?.startsWith(redirectUri)) throw new Error(`Sign-in did not redirect to the callback (HTTP ${submitted.status})`);
  const code = new URL(location).searchParams.get("code");
  return { code, verifier };
}

const allUserScopes = Object.keys(USER_SCOPES).join(" ");
try {
  const { code, verifier } = await pkceSignIn(`openid ${allUserScopes}`);
  {
    const wrong = await token({ grant_type: "authorization_code", client_id: "quizarena-client", code, redirect_uri: redirectUri, code_verifier: b64url(randomBytes(32)) });
    check("Code exchange with a wrong PKCE verifier refused", wrong.status === 400 && !wrong.body.access_token, `error=${wrong.body.error}`);
  }
  // The code above is now spent; sign in again for the positive exchange.
  const second = await pkceSignIn(`openid ${allUserScopes}`);
  const { status, body } = await token({ grant_type: "authorization_code", client_id: "quizarena-client", code: second.code, redirect_uri: redirectUri, code_verifier: second.verifier });
  check("PKCE S256 sign-in exchanges code for tokens", status === 200 && Boolean(body.access_token), `HTTP ${status}`);
  if (body.access_token) {
    const access = decode(body.access_token);
    const expectedAud = [...new Set(Object.values(USER_SCOPES))].sort();
    check("User token carries all six resource audiences", JSON.stringify(audiences(access).sort()) === JSON.stringify(expectedAud), `aud=${audiences(access).join(",")}`);
    check("User token carries every user scope", Object.keys(USER_SCOPES).every((s) => scopes(access).includes(s)));
    check("User token never carries machine scopes", !scopes(access).some((s) => s in MACHINE_SCOPES), `scope=${access.scope}`);
    check("User token identifies the browser client and a user actor",
      access.azp === "quizarena-client" && access.qa_actor === "user" && Boolean(access.sub) && access.iss === authority,
      `azp=${access.azp} qa_actor=${access.qa_actor}`);
    check("User access token signature verifies against JWKS", signatureValid(body.access_token));
    const id = decode(body.id_token);
    check("ID token audience is the client, not an API (cannot pass API audience checks)",
      JSON.stringify(audiences(id)) === JSON.stringify(["quizarena-client"]), `aud=${audiences(id).join(",")}`);
  }
  const narrow = await pkceSignIn("openid stats.read");
  const narrowTokens = await token({ grant_type: "authorization_code", client_id: "quizarena-client", code: narrow.code, redirect_uri: redirectUri, code_verifier: narrow.verifier });
  const narrowClaims = narrowTokens.body.access_token ? decode(narrowTokens.body.access_token) : {};
  check("Requesting one scope yields only that scope's audience",
    JSON.stringify(audiences(narrowClaims)) === JSON.stringify(["quizarena-statistics"]) && !scopes(narrowClaims).includes("leaderboard.read"),
    `aud=${audiences(narrowClaims).join(",")} scope=${narrowClaims.scope}`);
} catch (error) {
  check("PKCE S256 browser sign-in", false, error.message);
}

const failed = results.filter((r) => !r.ok).length;
console.log(`\n${results.length - failed}/${results.length} checks passed`);
process.exit(failed ? 1 : 0);
