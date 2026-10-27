# ADM Dashboard — Web beta 0.7

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

Font size scales interface text and map labels to 100%, 125%, or 150%. ADM highlighting marks systems strictly below 5.0 or 4.0 with orange rings; missing/NPC ADM is excluded and cached results are labeled. Alliance logos replace map nodes by default using the alliance ID in public sovereignty data and CCP’s image server; unavailable logos fall back to nodes. All display settings are remembered in this browser.

Sovereignty alliance names appear in selected-system details and map tooltips, resolved on demand from the public ESI alliance endpoint and cached for the session. Failed lookups display the alliance ID and retry after five minutes. No logo toggle is needed.

Map labels display system name, sovereignty-holding alliance name, and ADM on separate lines. Labels reposition to avoid collisions; zoom in for crowded systems. Alliance names refresh labels as lookups finish. NPC/unclaimed systems are identified explicitly.

## Wormholes and storms

A permanent right-hand desktop panel shows active Thera/Turnur connections and reported Metaliminal storm centers. On narrower screens it stacks below the map so it remains accessible without another tab. Wormholes refresh directly from Eve-Scout every two minutes; expired connections are removed locally. Storm snapshots are fetched during Pages builds and on a ten-minute GitHub Actions schedule (scheduling delays are possible). Each source shows its checked time; failures retain clearly labeled previous snapshots. Storm data older than 30 minutes is marked stale. Search/filter connections, copy signatures, or show a system on the map. These connections are not added to gate routing.

## Vulnerable skyhooks overlay

Enable **Vulnerable skyhooks** above the map. Pink diamonds and counts identify systems with currently open theft-vulnerability windows in the selected region; the right-hand list shows each planet ID and window closing time. Select Show on map to focus a system. Upcoming windows are excluded and expired windows disappear automatically.

Data comes directly from CCP's public /skyhooks/raidable endpoint using compatibility date 2026-05-19, without SSO. It refreshes every five minutes while the page is visible and respects Retry-After. Failed or older-than-ten-minute checks hide current highlights and show unknown/stale status instead of reporting zero. The feed is cached; verify in game. Public records provide planet IDs, not structure names.

Reference: https://esi.evetech.net/meta/openapi.json?compatibility_date=2026-05-19
