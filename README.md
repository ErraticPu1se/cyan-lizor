# Cyan Lizor

An unofficial Windows desktop pet featuring a cyan lizard from Rain World.

## Download

**[Download the Windows preview ZIP](https://github.com/ErraticPu1se/cyan-lizor/releases/download/v0.1.0-preview.1/CyanLizor-0.1.0-vocal-hops-win-x64.zip)** · [Release notes](https://github.com/ErraticPu1se/cyan-lizor/releases/tag/v0.1.0-preview.1)

Download the named **CyanLizor** ZIP, not GitHub's **Source code** archives or **Code → Download ZIP**.

## Requirements

- **Windows x64** and your own installed copy of **Rain World v1.11.8**.
- The game does not need to be running, but its installation must remain available. The app checks the game build's fingerprint; unsupported builds are rejected.
- No Unity Hub, Python, or Docker needed to run the download.

## Get started

1. Extract the **entire ZIP** to a writable folder. Keep all included files and folders together.
2. Run **CyanLizor.exe**.
3. Enter your Rain World installation folder—the folder containing `RainWorld_Data`—and click **Prepare local game assets**. Wait for preparation to finish.
4. Under **Your lizard**, enter an ID and click **Apply ID**, then **Start desktop pet**.
5. Keep the control panel open. **Stop desktop pet**, or close the panel, to stop the pet.

Preparation reads your game installation without modifying it. Game graphics and sounds are not included in the download.

## What you can do

- Choose a cyan's appearance by ID and adjust its desktop size.
- Watch it roam, rest, crawl, and propel itself, with personality-influenced decisions.
- Enable **Climb backgrounds and window edges** to let it grip and follow visible windows.
- Move the cursor nearby for head attention; hold the pet for **0.1 seconds** to grab it, then release to drop.
- Enable **Explore where I click** and **Allow cyan propulsion**: nearby clicks invite direct hops; farther clicks invite longer hops that stop short of the cursor. Very short distances use crawling, and jumps require a valid trajectory.
- Hear cyan calls and jump sounds, with head glow and propulsion particles. Calls have a **10-second cooldown**; idle calls wait **30–60 seconds of idle time**. Sound can be muted.

## planned update

feeding, multiple pets, more lizard species
## Help and source

[Report a problem](https://github.com/ErraticPu1se/cyan-lizor/issues) with your Windows version, display scaling, lizard ID, and reproduction steps. The desktop log is at `%USERPROFILE%\AppData\LocalLow\ErraticPu1se\Cyan Lizor Preview\desktop-player.log`.

[Developer build instructions](docs/development.md) · [Source for this release](https://github.com/ErraticPu1se/cyan-lizor/tree/v0.1.0-preview.1)

Rain World belongs to its respective creators and rights holders. This unofficial project does not distribute game assemblies, extracted graphics, audio, or decompiled sources. Third-party dependency notices accompany the download.
