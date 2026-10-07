# SMT Mac Beta 0.6 — Apple Silicon

A native macOS beta built from the local v0.3 client, covering the planned v0.4–v0.6 feature groups. Includes the self-contained `.app`; no Terminal launcher or .NET installation is required.

## Install

1. Quit the previous SMT Mac Beta.
2. Unzip the package and move **SMT Mac Beta.app** into Applications, replacing the earlier beta.
3. Open the app. This is ad-hoc signed, not Apple-notarized. If macOS blocks it, use System Settings → Privacy & Security → Open Anyway after reviewing the app's origin.
4. Allow Documents access when requested. Intel automatically scans `~/Documents/EVE/logs/chatlogs` every two seconds. If an old custom folder was saved, click **Use automatic EVE folder**.

Existing region and log settings are retained. Login is optional. Public ADM, activity, wormholes, storms, kills and local logs work without it.

## What's included

| Area | Beta behavior |
|---|---|
| Characters | Browser-based EVE SSO with PKCE; multiple characters; location, ship and online status; select/follow on the map; reconnect and disconnect |
| Credentials | Refresh tokens in native macOS Keychain; signature/audience/expiry validation; automatic access-token refresh |
| Fleet | Fleet member locations and ships where ESI authorizes the connected character, normally the fleet boss; roster selection focuses the map |
| Kills | zKillboard R2Z2 feed from connection onward; current-region list; recent kill markers; double-click opens the killmail |
| Activity | ESI last-hour ship/pod/NPC kills and ship jumps; proportional map rings; counts on selected-system details |
| Wormholes | Public Eve-Scout Thera/Turnur connection lists, known-space endpoint markers, signatures, ship-size limit and expiry |
| Storms | Eve-Scout Rescue storm centers/types; strong one-gate and weak three-gate areas |
| Ansiblex | Import bidirectional links from a simple CSV; persist the network; purple map links; optional inclusion in shortest-hop routing |
| Capitals | Hull class and Jump Drive Calibration; real 3D light-year distances; minimum-jump routes; per-leg and total LY; highsec destinations and Pochven excluded |
| Route control | Ordered comma-separated waypoints and avoided systems; gate-only, gates+Ansiblex, or capital routing |
| Existing features | Regional map, search, public sovereignty ADM/indexes, automatic local intel, channel filtering and manual reports |

## One-time EVE SSO setup

No shared EVE application client ID has been registered for this beta. Before testing characters/fleets:

1. Open https://developers.eveonline.com/applications and register a native / PKCE application.
2. Set this callback **exactly**, including the trailing slash:

   `http://localhost:17386/callback/`

3. Enable all four scopes:

   - `esi-location.read_location.v1`
   - `esi-location.read_ship_type.v1`
   - `esi-location.read_online.v1`
   - `esi-fleets.read_fleet.v1`

4. Copy the **public client ID** into the app's **Pilots** tab. Do not enter a client secret; PKCE does not use one.
5. Choose **Log in with EVE Online**, complete authorization in your browser, and return to the app. Repeat to add another character. Only one app instance can listen on the callback port at a time.

The callback listener runs only during login, accepts loopback requests with the correct random state, and times out after four minutes. EVE passwords never enter this app. Keychain may request permission; allow the app to access its own token entries. Disconnect removes the local token; to revoke the grant at EVE, use https://developers.eveonline.com/authorized-applications.

ESI limits visibility. Being a regular fleet member does not necessarily grant the complete fleet roster. Use the fleet boss character for fleet testing. Characters in wormhole systems outside the bundled known-space map remain listed by system ID and cannot be focused on a regional map.

## Ansiblex import

Create a UTF-8 text/CSV file with **two exact system names per line**, separated by a comma. No header row. Lines starting with `#` are comments. Each row is a bidirectional link; importing replaces the saved network only after every row validates. See `ansiblex-template.csv`.

Select **Gates + Ansiblex** after import. Access lists, operational status, fuel, ship restrictions and structure IDs are not checked. Only import links you know you can use. This beta does not discover private alliance networks automatically.

## Capital planning

Select **Capital jumps**, choose the hull class and JDC level, then supply start/end and optional waypoints/avoidance. The planner minimizes jump count; it does not optimize fuel, fatigue or total LY among equal-hop paths. Jump freighters and Black Ops may depart highsec, but all jump destinations must be eligible low/null systems. It uses the included upstream coordinate snapshot, independent of map layout coordinates.

A geometrically reachable system is not a guaranteed usable cyno. Confirm cynos, jammers, beacons, docking, access, fuel, ship restrictions and fatigue in game. Pochven and restricted Jovian regions are excluded as destinations. Routes across regions are visible in the route list; the regional canvas shows legs whose endpoints are in view.

## Refresh and freshness

- Intel: every 2 seconds; reports expire on-map after 15 minutes.
- Characters/fleet: polling loop every 6 seconds, subject to ESI cache. Offline positions are explicitly last locations. Failed character updates are marked stale; map markers expire after two minutes without a successful poll.
- Activity: checked every 5 minutes; ESI data is an hourly aggregate and may be cached longer.
- Wormholes: every 2 minutes; expired connections are removed. Mass and actual collapse are not independently verified.
- Storms: every 15 minutes; depends on the public HTML table. A parsing or network failure is displayed and retained data may be stale.
- Kills: polls about every 6 seconds and catches up in bounded batches. Public reports may arrive late; list retains up to 300 from the last hour, markers last 15 minutes. This is not a full historical killboard.
- ADM: existing 5-minute cached ESI behavior.

Rate-limit backoff and server cache expiry are respected. Feed status labels distinguish errors from successful checks. The beta relies on external services, which can change independently.

## Suggested hands-on tests

1. Launch with EVE running and send a system name in your intel channel. Confirm automatic appearance; restart the app and try a new chat session.
2. Add two characters. Move one through a gate, change ships, and test follow mode. Restart to test Keychain-backed token refresh. Disconnect one and confirm its marker disappears.
3. Connect the fleet boss. Verify another member's system and ship against EVE. Leave/disband the fleet and verify the roster clears.
4. Switch all four activity layers. Inspect counts in selected-system details. Open a killmail from the current-region list.
5. Select a Thera/Turnur connection and a storm. Compare signatures, expiry and locations with the public sources.
6. Import a known Ansiblex CSV, compare gate-only and bridge-enabled routes, then test an avoided endpoint and a waypoint.
7. Plan a known capital route for your hull/JDC. Confirm every leg is within range. Compare to in-game range and existing operational routes.

For a bug report include macOS version, selected tab/region, exact steps, status message and a screenshot. Never include refresh/access tokens or authorization callback URLs.

## Known beta boundaries

EVE account authorization and actual fleet permissions require your hands-on test; no developer account or credentials were supplied. This is not complete Windows SMT parity. No in-game waypoint writes, standings overlays, private wormhole mapping, automatic bridge discovery, fuel/fatigue simulation, alert sounds, desktop overlay, auto-updater, Intel-Mac package or Developer ID notarization. Website-only alliance logos, font scaling and ADM threshold filters were not carried into the v0.2 local baseline.

## Build and sources

.NET 8, Avalonia 11.3.8. `bash packaging/build-mac.sh osx-arm64` builds the self-contained app on macOS. Tests live under `tests/`; the workflow builds and validates on an Apple Silicon macOS runner.

Source branch: https://github.com/partiizan/adm-dashboard/tree/mac-local-v0.6/local-client

Bundled universe, 3D coordinates and regional layouts derive from Slazanger/SMT commit `6b3b4c6a213a349549ef737c89584fc3f048033b`. Retain `UPSTREAM-LICENSE.txt`.

Service references:
- EVE SSO: https://developers.eveonline.com/docs/services/sso/
- Public ESI: https://esi.evetech.net/
- Eve-Scout: https://api.eve-scout.com/v2/public/signatures?system_name=Thera
- Storms: https://evescoutrescue.com/home/stormtrack.php
- zKillboard R2Z2: https://r2z2.zkillboard.com/ephemeral/sequence.json
