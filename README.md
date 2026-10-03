# ADM Dashboard — Web beta 0.3

A static browser companion for EVE Online, designed for GitHub Pages. Includes 5,202 systems across 68 regional maps, shortest stargate routing, live public sovereignty ADM and development indexes, manual intel, and local UTF-8/UTF-16 chat-log import. No ESI login or server required.

## Run and build

Use Node 22 or newer. No npm dependencies to install.

```sh
npm test
npm run build
python3 -m http.server 4173 --directory dist
```

Open http://localhost:4173. Serve over HTTP; opening index.html directly as a file does not support module/data loading.

## GitHub Pages

1. Create a repository and push these files to its `main` branch.
2. In Settings → Pages → Build and deployment, select **GitHub Actions**.
3. Run the included **Publish ADM Dashboard** workflow or push a commit to main.

The workflow tests and builds the site, uploads `dist`, and deploys it to Pages. All asset URLs are relative, supporting a project URL such as `https://partiizan.github.io/adm-dashboard/`. No secrets are required. GitHub Pages and Actions must be permitted for the repository/account.

## Data and privacy

- Regional layouts and gates are a bundled snapshot from Slazanger/SMT commit `6b3b4c6a213a349549ef737c89584fc3f048033b`. Source: https://github.com/Slazanger/SMT . Upstream MIT notice is included.
- Public ADM endpoint: `https://esi.evetech.net/sovereignty/systems`, compatibility date `2026-05-19`. Polling is about every five minutes while visible. Failures keep cached values visibly marked. Missing ADM is not treated as zero.
- Settings and public ADM cache use localStorage. Intel stays in tab memory and disappears on reload. Imported files are never uploaded. GitHub receives ordinary site requests; CCP receives public ESI requests.
- Import up to ten .txt files per batch, each up to 5 MB. Up to 500 reports retained; newest 100 displayed. Intel requires complete system names, and map highlights expire after 15 minutes. Imported timestamps are treated as UTC.
- Routes use the bundled stargates, not wormholes or jump bridges. High-security-only routing uses EVE's displayed high-security threshold. Verify routes in game.
- No live folder watching, shared intel, EVE SSO, ship tracking, or desktop overlay. ADM may be delayed by CCP caching. This is a beta, not a safety guarantee.

Unofficial project; not endorsed by CCP or SMT's maintainers. EVE Online belongs to CCP.
