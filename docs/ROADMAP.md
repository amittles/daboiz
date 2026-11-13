# Roadmap

## Phase 1 — offline demo (this commit set) ✅
- Runtime bootstrap, menu, settings, pause, party mode with a points tally
- Source/STRAFTAT movement (bhop, air-strafe, slide, surf)
- Data-driven weapons + modifiers, 69 guns
- Shared damage/knockback/status system with per-mode rules; reactive physics props
- Bots for every mode
- Minigames: Gun Bowling, Office HR, Red Light Green Light, Surf Race, Falling Chessboard (+ Sandbox)

## Phase 1.5 — first in-editor pass (do next)
- Open the project in 2022.3, fix any import or compile surprises, play every mode and tune:
  - the motor's feel (air-accel, friction, surf clipping, slide), camera FOV kick and recoil
  - the bots' surf line, the bowling aim, Office HR suspicion and HR aggression
  - the falling-tile pacing and the Red Light Green Light threshold
- Profile prop-heavy scenes (the Sandbox pyramids) and the IMGUI cost
- Add a `Boot` scene to Build Settings and make sure the Standard shader is included in builds

## Phase 2 — online multiplayer
- Netcode for GameObjects (NGO) + Unity Transport, with **Unity Relay + Lobby** for invite codes (or Steamworks /
  Facepunch transport for Steam friends)
- Server-authoritative `ActorCommand` input (it is already a struct), client prediction for `CharacterMotor`, and
  lag-compensated hitscan
- Replicate minigame state through a small `NetworkMinigame` wrapper; ranking and party points stay server-side
- Lobby UI: ready-up, mode voting, the party playlist, and bots filling empty slots

## Phase 3 — content and art
- An art pass: proper bean characters and hats, gun models, office, bowling alley and surf map kits, real SFX and music
- Move guns into ScriptableObjects (`WeaponDefinition` is already serializable) and add a modifier stacking UI for "mutators"
- 100+ guns: more Unreal Tournament, Halo and STRAFTAT riffs and more modifier combos (portal gun, grapple, gravity well)
- More minigames: King of the Hill on a moving platform, Hot Potato grenade, Paint Wars (goo coverage), Floor is Lava, Golf-with-guns
- Cosmetics, stats and the end-of-party awards ("Most Yeeted", "HR's Favourite")

## Known risks / tech debt
- Only verified by compiling against reference assemblies. It has never been run inside Unity.
- With `com.unity.inputsystem` in the manifest, Unity may offer to switch input backends. Both are supported.
- Surf on a `CharacterController` depends on the clip-velocity hack in `OnControllerColliderHit` and needs tuning.
- IMGUI is fine for a prototype and should move to UI Toolkit or uGUI for release.
