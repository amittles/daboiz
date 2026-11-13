# daboiz

A goofy 6-player "friendslop" party FPS prototype in Unity 3D (built-in render pipeline): a
Mario-Party-style rotation of minigames, STRAFTAT / Source-style movement (bhop, air-strafe,
slide, surf) and 50+ silly data-driven guns. **Phase 1 is an offline demo: you vs. up to 5 bots.**

**Unity version:** 2022.3 LTS (`2022.3.50f1`). Unity 6 should also open it (it will prompt to upgrade).

## Quick start (no scene needed)

1. Install [Unity Hub](https://unity.com/download) and the **Unity 2022.3 LTS** editor.
2. In Unity Hub: **Projects > Add** (or **Add project from disk**) and pick this folder (the one with `Assets`, `Packages`, `ProjectSettings`).
3. Open it and let Unity import. If Unity asks **"enable the new Input System backends?"**, either answer is fine
   (the code supports both the old Input Manager and the new Input System). Answering *Yes* restarts the editor.
4. Press **Play** in any scene, including the default *Untitled* one. The game builds itself at runtime
   (`GameBootstrap` uses `[RuntimeInitializeOnLoadMethod]`), so you don't need a scene, prefab or asset.
   It turns off any cameras already in the scene and uses its own.

To make a **standalone build**, save any scene (even an empty one) into **File > Build Settings > Scenes In Build**.
All materials use the built-in **Standard** shader, which is created at runtime. If objects show up pink in a build, add
`Standard` under **Project Settings > Graphics > Always Included Shaders**.

## Modes

| Mode | What you do |
|---|---|
| **Gun Bowling** | Turn-based bowling, but you shoot the pins. Each frame hands everyone a random goofy gun. 2 frames, 3 shots per frame. Strikes give bonus points. |
| **Office HR** | Short Among Us. One player is secretly HR and carries a hidden one-shot stapler with a long cooldown. Workers hold **E** at their task pads, report bodies (**E**), call the emergency button in the break room, and vote. Workers win if every task gets done or HR gets voted out. HR wins if only one worker is left or the clock runs out. |
| **Red Light Green Light** | A movement obstacle course: gaps, ramps, a crouch tunnel, hurdles and a launch ramp. Anyone still moving on red gets yeeted. Finish first. |
| **Surf Race** | CS-style surf ramps, 2 laps. The shove shotgun only knocks people back. Pick up speed, low gravity, shield and ammo. |
| **Falling Chessboard** | Arena tiles shake and fall faster and faster. The guns only knock people around and impair them (freeze, shrink, reverse controls, goo), so they never do damage. Last bean standing wins. |
| **Sandbox** | No timer. Every gun, physics props (can pyramids, crates, explosive barrels, pins), target dummies and a movement course. **P** resets the props and **B** toggles whether the bots fight. |
| **Party** | Plays all 5 minigames in a random order. Placement points go 10/7/5/3/2/1. The highest total wins. |

The bot count (0–5), auto-bhop, sensitivity, FOV, head bob, invert-Y and volume are all on the **Settings** screen. They are saved with PlayerPrefs.

## Controls

| Key | Action |
|---|---|
| WASD / Mouse | Move / look |
| Space | Jump. Hold it to bunny-hop when auto-bhop is on. |
| Ctrl / C / Shift | Crouch. Crouching at speed starts a slide. |
| LMB | Fire. Some guns charge up or fire a beam while you hold it. |
| R | Reload |
| 1–9, mouse wheel, Q, [ ] | Switch weapon |
| E | Interact (tasks, report body, emergency button) |
| Tab | Scoreboard |
| Esc | Pause menu: resume, restart, settings, main menu |
| Click / Space while dead | Switch which player you spectate |

**Movement tips:** jumping on landing keeps your speed. In the air, strafe (A/D) while turning the mouse the same way to gain
speed. On steep ramps (surf), hold the strafe key *into* the ramp and don't press W.

## Code layout

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for how it fits together and [`docs/ROADMAP.md`](docs/ROADMAP.md) for what comes next.

```
Assets/Scripts/
  Core/        bootstrap, game flow + menus, rules, input shim, runtime materials/audio/fx, helpers
  Movement/    Source-style CharacterMotor (+ tunables)
  Actors/      Actor (health, status, death), visuals, factory, ActorCommand input struct
  Player/      local input -> ActorCommand, first-person camera rig + viewmodel
  AI/          BotBrain (intent-based bots driven by each minigame)
  Combat/      damage/knockback interfaces, HitResolver, status effects
  Weapons/     WeaponDefinition (data), modifiers, registry (69 guns), holder, projectiles
  Props/       physics props, goo puddles, target dummies
  Minigames/   MinigameBase + 5 minigames + sandbox, LevelKit for procedural levels
  UI/          IMGUI HUD helpers
```

## Folder layout

- `Assets/Scripts` holds the gameplay code. The whole game is code-generated.
- `Assets/Scenes`, `Assets/Prefabs`, `Assets/Materials`, `Assets/Models` and `Assets/Audio` are reserved for the art pass.

Binary assets (fbx, png, psd, wav and similar) are configured for Git LFS in `.gitattributes`. Run `git lfs install` once on your machine before committing large binaries.
