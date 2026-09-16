# Set Up an Angular Project to Run on Custom Ports

This runbook configures a local Angular frontend and API to run on ports other than their defaults, without editing the proxy file for every run.

## Defaults

- Angular frontend: `4200`
- API: `3000`

## One-time setup

### 1. Make the Angular proxy read `API_PORT`

Replace the static JSON proxy configuration with `apps/web/proxy.conf.mjs`:

```js
const apiPort = process.env.API_PORT ?? '3000';

export default {
  '/api': {
    target: `http://localhost:${apiPort}`,
    secure: false,
    changeOrigin: true,
  },
};
```

Update the Angular workspace's serve target, or its `start` script, to use `proxy.conf.mjs` instead of `proxy.conf.json`:

```json
"start": "ng serve --proxy-config proxy.conf.mjs"
```

### 2. Parameterize the combined launcher

Set the root `dev` script to a small Node launcher that starts both processes with matching settings:

```json
"dev": "node scripts/dev.mjs"
```

The launcher should accept `--port` (Angular) and `--api-port` (API), defaulting to `4200` and `3000`. It must set `API_PORT` for the Angular proxy, `PORT` for the API, and `WEB_ORIGIN` to the Angular URL.

## Run on custom ports

Start both services with explicit port parameters:

```powershell
npm run dev -- --port 4300 --api-port 3001
```

The application is available at `http://localhost:4300`; Angular proxies `/api` to `http://localhost:3001`.

## Run services separately

In one terminal, run the API:

```powershell
$env:PORT = 3001
$env:WEB_ORIGIN = 'http://localhost:4300'
npm run dev --workspace @toss-and-tumble/api
```

In another terminal, run Angular:

```powershell
$env:API_PORT = 3001
npm run start --workspace @toss-and-tumble/web -- --port 4300
```

## Keep the defaults

Omit all parameters to use frontend port `4200` and API port `3000`:

```powershell
npm run dev
```

## Troubleshooting

- Use `npm run dev -- --port 4300 --api-port 3001`, not `npm run dev --port 4300`. The first `--` forwards options through npm.
- `--port` and `--api-port` require numbers from `1` through `65535`.
- If `/api` requests fail after changing the API port, confirm `API_PORT` and `PORT` use the same value.
- If requests are rejected by CORS, set `WEB_ORIGIN` to the exact Angular URL, including its port.
