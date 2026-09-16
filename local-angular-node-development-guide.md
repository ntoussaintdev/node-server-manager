# Local Angular + Node Development Guide

## Purpose

Use this workflow to develop each Angular/Node website locally before it is deployed as its own Docker Compose stack on the Netcup VPS. It deliberately avoids production-only infrastructure unless a test needs it.

## What to install

- Git
- The Node.js major version used in the production Dockerfiles (currently Node 22 in the planned stack)
- Docker Desktop with the WSL 2 backend

Docker Desktop is recommended, but it is not required for ordinary frontend/API editing. It is needed to build the production images locally, run Docker Compose, use local Supabase, and run local email capture.

## Do not run Caddy locally by default

Do not install or configure Caddy for initial local development. Run the Angular dev server and Node API on direct localhost ports instead:

```text
Angular frontend  http://localhost:4200
Node API          http://localhost:3000
Mailpit UI        http://localhost:8025  (when enabled)
```

Add local Caddy only when a specific test needs production-like reverse-proxy routing, HTTPS, headers, or multiple local hostnames. It is not required to build features.

## Per-site repository structure

```text
site-name/
├─ frontend/
│  ├─ src/
│  ├─ package.json
│  ├─ Dockerfile
│  └─ proxy.conf.json
├─ backend/
│  ├─ src/
│  ├─ package.json
│  └─ Dockerfile
├─ compose.dev.yaml
├─ compose.yaml
├─ .env.example
└─ .env.local                 # ignored; never commit
```

Use project-local commands and dependencies. Do not rely on global Angular or backend CLI installations.

## Normal native workflow

Install dependencies once in each application folder:

```powershell
Set-Location frontend
npm ci

Set-Location ../backend
npm ci
```

Run the applications in separate terminals:

```powershell
# Terminal 1
Set-Location frontend
npm run start

# Terminal 2
Set-Location backend
npm run dev
```

Angular should send browser API traffic to `/api/*`, not directly to `http://localhost:3000`. Add an Angular dev-server proxy so this request shape matches production without CORS or Caddy:

```json
{
  "/api": {
    "target": "http://localhost:3000",
    "secure": false,
    "changeOrigin": true
  }
}
```

Start Angular with the matching `--proxy-config proxy.conf.json` option, normally wired into the project's `npm run start` command.

## Environment files and secrets

Keep real local values only in `.env.local`, which must be ignored by Git. Commit `.env.example` with placeholder names only.

```text
Frontend:
  SUPABASE_URL
  SUPABASE_PUBLISHABLE_KEY

Backend:
  SUPABASE_URL
  SUPABASE_SECRET_KEY
  MAIL_TRANSPORT=mailpit
  MAILPIT_HOST=localhost
  MAILPIT_PORT=1025
```

The Angular frontend may contain only the Supabase URL and publishable key. Supabase secret/service keys, Mailgun credentials, webhook secrets, and payment secrets belong only to the Node API environment.

## Authentication and Supabase

For local Auth testing, add the exact local callback URL to Supabase Auth redirect URLs:

```text
http://localhost:4200/auth/callback
```

Do not use the production Supabase project for exploratory migrations, seed data, or destructive tests. Prefer, in order:

1. A separate development Supabase project, if available.
2. Local Supabase through the Supabase CLI and Docker Desktop for schema/RLS/migration work.
3. A restricted, deliberate integration check against production only when no schema/data change is involved.

Every browser-exposed table and storage path needs Row Level Security policies plus tests for permitted and denied access.

## Docker development workflow

Use Docker when checking production parity. A development Compose file should mount source folders and expose only local ports, for example frontend `4200:4200`, API `3000:3000`, and Mailpit `8025:8025`/`1025:1025`.

```powershell
docker compose -f compose.dev.yaml up --build
```

Use the production Compose file separately to verify the final multi-stage Angular build, Nginx static serving, Node runtime, health checks, and container networking. Do not expose production application ports on the VPS; Caddy is the only public web entry point there.

## Email testing

Use Mailpit locally. It catches messages rather than delivering them to real recipients.

```text
Node API → Mailpit SMTP → Mailpit web UI
```

Do not use Mailgun for ordinary local development. Reserve it for explicitly approved staging or production-like delivery verification. Mailgun API keys must never be checked into the repository or added to browser configuration.

## Before pushing or deploying

1. Run the frontend production build.
2. Run backend tests and lint/type checks.
3. Verify the API health endpoint.
4. Run Supabase migration and RLS tests against an isolated environment.
5. Send a test email to Mailpit.
6. Build the frontend and API Docker images locally.
7. Confirm no `.env.local`, API keys, logs, or generated secrets are tracked.

## Production relationship

Production uses one Compose project per site with unique shared-network aliases, for example `tossandtumble-frontend` and `tossandtumble-api`. CI builds immutable, version-tagged images; the VPS pulls those images and rolls back by changing the version tag.

Caddy routes the public domain to that site's frontend and `/api/*` to its Node API. Mailcow owns direct SMTP/IMAP ports and forwards human mailbox outbound mail through Mailgun. Application transactional mail goes directly from the Node API to Mailgun.
