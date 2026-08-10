# Changelog

All notable changes to All Seeing God are documented here.

## 1.0.4

- Incremented the package version for a new Thunderstore release.
- Added the required 256 x 256 package icon.
- Added the project website URL to the package manifest.
- Updated the BepInEx dependency to `5.4.2305`.

## 1.0.3

- Keep the plugin runner alive across menu, lobby, and level scene changes.
- Match the lifecycle used by current R.E.P.O. v0.4 minimap and god-mode plugins.
- Raise the diagnostic overlay draw priority.

## 1.0.2

- Avoid unreliable local `PhotonView.IsMine` detection under CrossOver/Wine.
- Add an on-screen health diagnostic and clearer runtime logging.
- Apply health protection to active `PlayerHealth` instances.

## 1.0.1

- Reapply health after `PlayerHealth.Update` to avoid same-frame synchronization resets.
- Support the native `godMode` flag and the newer `HurtOther` damage path.

## 1.0.0

- Initial implementation: god mode, health/stamina enhancement, always-on map, enemy markers, and valuable markers.
