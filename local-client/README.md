# SMT Mac Beta · 0.3.0

An unofficial, experimental Apple Silicon desktop prototype based on the data in Slazanger's EVE Map Tool. It is a new Avalonia frontend and portable core, **not a complete port of SMT**. No affiliation or endorsement by Slazanger or CCP is implied.

## Try the included application

1. Quit the previous beta. Extract the ZIP and drag **SMT Mac Beta.app** to Applications, replacing the previous copy if present.
2. Double-click **SMT Mac Beta.app**. It is a compiled, self-contained Apple Silicon application, already ad-hoc signed. No shell launcher, Terminal command, or .NET installation is needed.
3. This prototype has no Apple Developer ID/notarization. If macOS blocks it, approve this specific app through **System Settings → Privacy & Security → Open Anyway**, if offered, then open it again. Do not disable global security settings.
4. ADM loads automatically from CCP's public ESI service; no account login is required. Toggle **ADM** above the map to show or hide the labels. Click a system for ADM and the military, industrial, and strategic indexes.
5. Click **Try demo** to explore Delve with synthetic intel reports. ADM remains real ESI data even in intel demo mode. Exit demo to resume local log monitoring.
6. Intel starts automatically from `~/Documents/EVE/logs/chatlogs`, using the current macOS account. Allow Documents access if macOS asks. No connector or ESI login is required. A saved custom folder from v0.2 is still honored; click **Use automatic EVE folder** to return to the standard location. **Change log folder…** is only needed for a nonstandard installation.
7. If the folder does not exist yet, the app waits and retries every two seconds. Start EVE with chat logging enabled and join your intel channels. Log content stays on your Mac.

The bundle is self-contained: no .NET installation required for running it. Target: Apple Silicon, macOS 12 or newer, subject to real-machine validation. Intel Macs require a separate `osx-x64` build from source.

## What works in this build

- Public ESI ADM labels (1.0–6.0×), development indexes and capital-system indicator. Automatic five-minute checks, ETag revalidation, server rate-limit handling and persistent cached values. Cached/unverified values are explicitly marked; N/A means no applicable sovereignty ADM and — means unavailable/not reported.
- 5,202 systems and 68 regional layouts from the pinned SMT snapshot.
- Region selection, mouse/trackpad scrolling to zoom, dragging to pan, and Fit.
- System search across all regions; click a node or search result to inspect it.
- Shortest stargate routing across regions, with an optional high-security-only restriction. Set endpoints from selected systems or type their complete names. Click route steps to view their regions.
- Local intel ingestion, polling every two seconds. UTF-8, UTF-16LE and UTF-16BE log files are supported. Split writes are buffered; rotated/new files are discovered.
- Exact full system-name matching, manual reports, recent-intel highlighting and reported-clear indicators.
- Settings persist locally. Logs are read only. No log data or credentials are uploaded. The app requests only the public sovereignty dataset from `https://esi.evetech.net/sovereignty/systems`, using compatibility date `2026-05-19`.

## Boundaries that matter

- **No EVE SSO, character/location tracking, kill feeds, alerts/sound, Ansiblex routing, overlays, or global shortcuts in 0.3.** This is the map/intel foundation for those later features.
- The bundled map is a static upstream snapshot. Verify routes in game. Only listed stargates participate in routing; no jump bridges, wormholes or jump-drive paths. The high-sec option uses SMT's security threshold of 0.45.
- Intel is text reported by people, not confirmed hostiles or confirmed safety. The parser recognizes exact system names, including names containing spaces; abbreviated names are not resolved. Ordinary words that are also system names may match.
- Recent reports within 15 minutes are ingested when monitoring starts; older reports are not replayed. Files not modified in two days are ignored. Up to the last 1 MiB of historical content is read initially. The UI retains at most 500 reports and displays the newest 100. Map rings expire after 15 minutes even while paused.
- A report mentioning multiple systems applies its reported-clear status to all those names. Mixed clear/hostile sentences are conservatively treated as reports. Use manual correction if text is ambiguous.
- Demo mode suspends live monitoring and clears its synthetic reports when exited. Demo and real reports are not mixed.
- Test with your actual EVE client and chat channels before relying on it. The user confirmed 0.1 runs correctly on their Mac. v0.3 is built and tested on a macOS CI runner; actual EVE-generated files and macOS Documents permission prompts still need confirmation on your Mac.

## Validation performed

- Release compilation and self-contained `osx-arm64` publication.
- Automated core checks (including automatic folder discovery, late folder creation, username-independent paths, override handling and locked-file isolation): real map references, known routes, adjacency, high-sec restrictions, search, exact/multiword intel matching, timestamps, encoding, incremental reads, incomplete lines, truncation and new files; plus ADM parsing, missing/NPC values, cache revalidation, outages and rate limits.
- Headless Avalonia test on macOS: app starts without configured connector, waits for a missing default folder, ingests a newly created UTF-16 log, follows appended reports and new session files, and resumes monitoring after demo.
- Apple Silicon publication and macOS code-signature verification. Native window interaction and Documents access prompts still need your confirmation.

## If it does not launch

To capture a startup error, run the executable directly from Terminal (adjust the path to where you extracted the download):

```sh
"/path/to/SMT Mac Beta.app/Contents/MacOS/SmtMac"
```

Send the terminal output and your macOS version. The application does not require administrator privileges.

## Build from source

Install Microsoft's .NET 8 SDK, then in `source`:

```sh
dotnet run --project src/Smt.Desktop
dotnet run --project tests/Smt.Tests -- data/universe.json
bash packaging/build-mac.sh osx-arm64
```

For Intel Macs use `osx-x64`. The build script creates the app under `dist/<runtime>/` and ad-hoc signs it when run on a Mac. Developer ID signing/notarization for public distribution is not configured.

The UI smoke test runs with:

```sh
dotnet run --project tests/Smt.Smoke -- preview.png
```

Structure: `Smt.Core` contains data, navigation, parsing and file handling without any UI dependency; `Smt.Desktop` contains Avalonia rendering and window orchestration; `tests` contains executable regression and smoke checks. Package versions are pinned in project files. `tools/import_smt_data.py` reproducibly converts upstream XML to JSON.

## Attribution

SMT source: https://github.com/Slazanger/SMT

Data snapshot commit: `6b3b4c6a213a349549ef737c89584fc3f048033b`.

The original SMT MIT notice is preserved in `UPSTREAM-LICENSE.txt`. EVE Online and its universe remain the property of CCP; upstream map layouts may incorporate third-party material, so verify their redistribution terms before wider publication. Avalonia/.NET and their dependencies retain their own licenses. This bundle is a private evaluation prototype, not an official SMT release.
