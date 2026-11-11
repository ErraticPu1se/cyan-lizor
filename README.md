# Cyan Lizor

A desktop cyan lizard.

It roams, rests, climbs, observes the cursor, and propels itself across the screen. Its decisions are influenced by personality and current state. It will not always cooperate. That is not a defect.

Developed by **ErraticPu1se**. Any resemblance between that name and another iterator is irrelevant.

## Download

**[Download the Windows preview ZIP](https://github.com/ErraticPu1se/cyan-lizor/releases/download/v0.1.0-preview.2/CyanLizor-0.1.0-pet-menu-win-x64.zip)** · [Release notes](https://github.com/ErraticPu1se/cyan-lizor/releases/tag/v0.1.0-preview.2)

Download the packaged **CyanLizor** ZIP.

Do not use GitHub’s **Source code** archives or **Code → Download ZIP**. They are not the application.

## Requirements

- **Windows x64**
- An installed copy of **Rain World v1.11.8**

Rain World does not need to be running. Its installation must remain available.

The app verifies the installed game build before preparing local assets. Unsupported builds are rejected.

## Setup

1. Extract the **entire ZIP** into a writable folder.
2. Keep all included files and folders together.
3. Run **CyanLizor.exe**.
4. Select the Rain World installation folder containing `RainWorld_Data`.
5. Click **Prepare local game assets** and wait for completion.
6. Under **Your lizard**, enter an ID and click **Apply ID**.
7. Click **Start desktop pet**.

Keep the control panel open while the pet is active.

Use **Stop desktop pet**, or close the control panel, to stop it.

## Behavior and interaction

- Enter an ID to select the lizard’s generated appearance.
- Adjust its desktop size.
- Allow it to roam, rest, crawl, and propel itself.
- Enable **Climb backgrounds and window edges** to let it grip and follow visible windows.
- Move the cursor nearby to attract its attention.
- Hold the lizard for **0.1 seconds** to grab it. Release to drop it.
- Hover directly above it for **0.7 seconds** to receive an affectionate call.
- Enable **Explore where I click** to give it destinations.
- Enable **Allow cyan propulsion** to permit directed jumps.

Nearby clicks invite direct hops. Distant clicks invite longer hops that stop short of the cursor. Very short distances use crawling. Invalid trajectories are rejected.

The lizard produces calls and propulsion sounds, accompanied by head glow and particles.

Calls have a **10-second cooldown**. Idle calls occur after **30–60 seconds** without activity. Sound may be muted.

## Right-click menu

Right-click the lizard to:

- Open **Settings**.
- Select **Small (1.0×)**, **Medium (1.5×)**, or **Large (2.0×)**.
- Toggle roaming.
- Toggle click exploration.
- Toggle sound.
- Stop the pet.

The lizard pauses while its menu is open. Changes are synchronized with the control panel.

Right-clicks outside the lizard remain available to other applications. Naturally.

## Planned additions

- Feeding
- Multiple simultaneous pets
- Additional lizard species

They are planned. They are not present in this preview.

## Problems and source

[Report defects through GitHub Issues](https://github.com/ErraticPu1se/cyan-lizor/issues).

Include:

- Windows version
- Display scaling
- Lizard ID
- Reproduction steps
- Expected behavior
- Observed behavior

Be precise. “It behaved strangely” conveys nothing.

The desktop log is located at:

```text
%USERPROFILE%\AppData\LocalLow\ErraticPu1se\Cyan Lizor Preview\desktop-player.log
```
