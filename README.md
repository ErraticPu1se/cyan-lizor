# Cyan Lizor

A desktop cyan lizard.

It roams, rests, climbs, observes the cursor, and propels itself across the screen. Its decisions are influenced by personality and current state. It will not always cooperate. That is not a defect.

Developed by ErraticPu1se. Any resemblance between that name and another iterator is irrelevant.

## Download

**[Download the Windows preview ZIP](https://github.com/ErraticPu1se/cyan-lizor/releases/download/v0.2.1-preview.1/Lizor-0.2.1-preview.1-win-x64.zip)** · [Release notes](https://github.com/ErraticPu1se/cyan-lizor/releases/tag/v0.2.1-preview.1)

Download the packaged **Lizor** ZIP.

Do not use GitHub’s **Source code** archives or **Code → Download ZIP**. They are not the application.

## Requirements

- **Windows x64**
- An installed copy of **Rain World v1.11.8**

Rain World does not need to be running. Its installation must remain available.

The app verifies the installed game build before preparing local assets. Unsupported builds are rejected.

Unity Hub, Python, and Docker are not required to run the packaged application.

## Setup

1. Extract the **entire ZIP** into a writable folder.
2. Keep all included files and folders together.
3. Run **Lizor.exe**.
4. Check the automatically detected Rain World folder. If it is not found, click **Browse** or paste the installation folder containing `RainWorld_Data`.
5. Click **Prepare local game assets** and wait for completion.
6. Under **Your lizard**, enter an ID and click **Apply ID**.
7. Click **Start desktop pet**.

Preparation reads your game installation without modifying it. Game graphics and sounds are not included in the download.

Automatic detection checks Steam libraries, including additional library locations. Other installations can be selected manually. If **Start desktop pet** is unavailable, the panel explains what still needs attention.

The compact dark-grey and cyan panel includes an animated preview and an always-visible Start/Stop control. Close the panel to keep Lizor running in the system tray. Double-click the **Liz** icon to reopen settings.

Use **Stop desktop pet** to stop the pet, or **Quit Lizor** in the panel or tray menu to exit the app.

Your appearance ID, game folder, size, climbing mode, roaming, propulsion, click exploration, and sound preferences are remembered between launches. The lizard still waits for you to press Start. Saved presets let you switch between lizards; one pet can be active at a time.

## Behavior and interaction

- Enter an ID to select the lizard’s generated appearance.
- Adjust its desktop size. **Medium (1.2×)** is the default and reset size.
- Allow it to roam, rest, crawl, and propel itself.
- Improved cursor responsiveness, steadier resting posture, and optimized memory usage.
- Choose **Edges only** to climb desktop borders and visible window edges.
- Choose **Edges + backgrounds** to also climb the desktop background and window interiors.
- Move the cursor nearby to attract its attention.
- Hold the lizard for **0.1 seconds** to grab it. Release to drop it.
- Hover or gently move over a resting lizard for **0.7 seconds** to receive an affectionate call.
- Enable **Explore where I click** to give it destinations.
- Enable **Allow cyan propulsion** to permit directed jumps.

Nearby clicks invite direct hops. Distant clicks invite longer hops that stop short of the cursor. Very short distances use crawling. Invalid trajectories are rejected.

Attached lizards follow their supporting window when it moves or resizes, including when resting on its interior. Closing or minimizing that window releases the lizard. Removing its support has consequences.

The body and tail react while being dragged. After release, it falls about one body length before reaching for a nearby window edge or desktop border. If nothing is within reach, it keeps falling. Landing briefly absorbs the impact and restores its footing before normal movement resumes.

The lizard produces calls, propulsion sounds, and landing impacts, accompanied by head glow and particles.

Ordinary calls have a **10-second cooldown**. Idle calls occur after **30–60 seconds** of idle time. Affection calls have a separate **3-second cooldown** during continued petting. Grabbing and open menus do not count as petting. Calls do not overlap. Sound may be muted.

## Right-click menu

Right-click the lizard to:

- Open **Settings**.
- Select **Small (1.0×)**, **Medium (1.2×)**, **Large (1.5×)**, or **Extra large (2.0×)**.
- Choose **Edges only** or **Edges + backgrounds** climbing.
- Toggle roaming.
- Toggle click exploration.
- Toggle sound.
- Stop the pet.

The lizard pauses while its menu is open. Changes are synchronized with the control panel.

Right-clicks outside the lizard remain available to other applications. Naturally.

## Planned additions

- Feeding
- Multiple simultaneous pets (they might fight with each other)
- Additional lizard species
- white lizard camo with background
- lizard spit might propel the cursor?
- mac support, but only with limited lizard presets.

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
%USERPROFILE%\AppData\LocalLow\ErraticPu1se\Lizor\desktop-player.log
```

[Developer build instructions](docs/development.md) · [Source for this release](https://github.com/ErraticPu1se/cyan-lizor/tree/v0.2.1-preview.1)

An unofficial project inspired by **Rain World**, created by Videocult and published by Akupara Games. This project does not distribute game assemblies, extracted graphics, audio, or decompiled sources. Third-party dependency notices accompany the download.
