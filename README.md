# Cyan Lizor

An unofficial Windows desktop pet featuring a cyan lizard from Rain World.

## Download

**[Download the Windows preview ZIP](https://github.com/ErraticPu1se/cyan-lizor/releases/download/v0.1.0-preview.2/CyanLizor-0.1.0-pet-menu-win-x64.zip)** · [Release notes](https://github.com/ErraticPu1se/cyan-lizor/releases/tag/v0.1.0-preview.2)

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
- Watch it roam, rest, crawl, and propel itself, with personality-influenced decisions. Landings briefly absorb impact and regain footing before normal movement resumes.
- Choose **Edges only** to climb desktop borders and visible window edges, or **Edges + backgrounds** to also climb the desktop background and window interiors. Attached pets follow window movement/resizing. Closing or minimizing the supporting window releases them; disabling background climbing releases unsupported pets without resetting them.
- Move the cursor nearby for head attention; hold the pet for **0.1 seconds** to grab it, then release to drop. The body and tail react to dragging, with bounded momentum on release.
- Hover or gently move over a resting lizard for about **0.7 seconds** for a cyan affection call. Continued petting has a **3-second cooldown** during continued contact; grabbing and menus do not count as petting. Affection has its own cooldown, so an ordinary call does not block petting after that call ends. Calls respect mute and never overlap.
- Enable **Explore where I click** and **Allow cyan propulsion**: nearby clicks invite direct hops; farther clicks invite longer hops that stop short of the cursor. Very short distances use crawling, and jumps require a valid trajectory.
- Hear light/heavy landing impacts, cyan calls and jump sounds, with head glow and propulsion particles. Calls have a **10-second cooldown**; idle calls wait **30–60 seconds of idle time**. Sound can be muted.

## Right-click controls

Right-click the desktop lizard to open its menu:

- Open **Settings**.
- Choose **Small (1.0×)**, **Medium (1.5×)**, or **Large (2.0×)**.
- Choose **Edges only** or **Edges + backgrounds** climbing; toggle roaming, click exploration, or sound.
- Stop the desktop pet.

The pet pauses while the menu is open, and changes also appear in the control panel. Right-clicks outside the pet still work normally in other apps.

## planned update

feeding, multiple pets, more lizard species
## Help and source

[Report a problem](https://github.com/ErraticPu1se/cyan-lizor/issues) with your Windows version, display scaling, lizard ID, and reproduction steps. The desktop log is at `%USERPROFILE%\AppData\LocalLow\ErraticPu1se\Cyan Lizor Preview\desktop-player.log`.

[Developer build instructions](docs/development.md) · [Source for this release](https://github.com/ErraticPu1se/cyan-lizor/tree/v0.1.0-preview.2)

Rain World belongs to its respective creators and rights holders. This unofficial project does not distribute game assemblies, extracted graphics, audio, or decompiled sources. Third-party dependency notices accompany the download.
