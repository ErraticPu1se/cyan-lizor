# Restored panel preview

The UI redesign from preview.1 has been rolled back. This branch uses the previous panel layout and theme, retaining the global mute and explicit next-launch preset fixes.

Advanced again shows the selected lizard's body specifications and cosmetic summary: head size, fatness, tail length, tail fatness, tail colour, cosmetic names, and personality. The preview has a taller viewport and its own 0.5x-3x zoom slider with Reset. Preview zoom adjusts only the preview camera, independently of desktop pet size. The preview render texture is 1024x384.

The original baseline remains at `archive/ui-before-refinement` (`e757742`). The rejected redesign remains in commit `0fbc2be`; the preview branch and PR now contain the restoration on top of it.

Global mute preserves each pet's sound preference. Use on next launch persists a saved-preset choice without switching live pets or redirecting later saves. Extra pets remain session-only.

## Validation and packaging

Runtime and editor/test assemblies compile against Unity 2020.3.45f1. Targeted runtime checks cover mute, startup-preset persistence and ownership, camera zoom/reset without changing desktop scale, and body-spec availability. Render checks inspect the restored panel, zoom levels, and Advanced for Cyan and White.

The Editor previously exited at LicensingClient IPC (199), so this experimental package uses the existing pinned 2020.3.45f1 Mono player shell with the rebuilt production runtime. It is not a fresh Editor export. Test harnesses, game assemblies, and local reference assets are excluded. Full Editor verification, keyboard navigation, and native tray/desktop interaction remain unverified.

