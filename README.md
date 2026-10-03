# ADM Dashboard — Web beta 0.5

A static browser companion for EVE Online, designed for GitHub Pages. Includes 5,202 systems across 68 regional maps, shortest stargate routing, live public sovereignty ADM and development indexes. No ESI login or server required.

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
- Routes use the bundled stargates, not wormholes or jump bridges. High-security-only routing uses EVE's displayed high-security threshold. Verify routes in game.

Unofficial project; not endorsed by CCP or SMT's maintainers. EVE Online belongs to CCP.


Settings and public ADM cache remain in browser storage. This dashboard does not access local files or ingest intel.

## Display controls

Font size scales interface text and map labels to 100%, 125%, or 150%. ADM highlighting marks systems strictly below 5.0 or 4.0 with orange rings; missing/NPC ADM is excluded and cached results are labeled. Alliance logos can replace map nodes using the alliance ID in public sovereignty data and CCP’s image server; unavailable logos fall back to nodes. All display settings are remembered in this browser.
