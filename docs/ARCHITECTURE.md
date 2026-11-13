# Architecture (phase 1)

Everything is built from code at runtime. There are no scenes, prefabs or ScriptableObjects yet, so the project
can't break from missing asset references, and gameplay data stays easy to diff. All code lives in the
`Daboiz.*` namespaces under `Assets/Scripts`.

## Boot and flow

- `Core/GameBootstrap`: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` creates one `GameFlow` object unless one
  already exists or `GameBootstrap.Disabled` is set.
- `Core/GameFlow` is the singleton state machine with the screens Menu, Settings, InGame and PartySummary. It owns
  - the camera and AudioListener, the HUD, and the `LevelRoot` transform that every level object is parented to,
  - `Modes[]`, the registry of minigames (title, tagline, factory),
  - loading a level: tear down the old root, build a `MatchContext` (participants, rng, settings), then call `MinigameBase.Begin`,
  - the pause menu, scoreboard, results, party tally, spectator chase cam, cursor locking and nametags,
  - `GameplayInputActive`, which says whether the local player's input should drive their actor.

## Minigames

`Minigames/MinigameBase` gives every mode its lifecycle: `Intro -> Playing -> Finished`, a round timer, a kill plane, a respawn
queue, scores and a ranking (`List<RankEntry>`, which party mode turns into points). Subclasses implement:

| Hook | Purpose |
|---|---|
| `Build()` | Build the level procedurally (`LevelKit`, `Prims`) and call `SpawnAllActors(loadout)` |
| `OnTick(dt)` | Mode logic while playing |
| `DriveBot(BotBrain)` | Fill in the bot's intents each frame |
| `CanFire(Actor)` | Per-mode fire gating (bowling turns, HR cooldown) |
| `HandleActorDied` / `OnActorFellOut` | Elimination, scoring, bodies |
| `DrawHUD(Hud)` / `ScoreLines()` | Mode HUD (IMGUI) and the Tab scoreboard |
| `Rules` | A `GameRules` instance: damage on/off, knockback on/off and scale, effects, respawn |

## Actors and input (netcode-ready split)

```
IActorController (PlayerInputController | BotBrain)
        |  GetCommand() -> ActorCommand  (move, yaw/pitch, jump, crouch, fire, reload, interact, weapon slot)
        v
Actor.Update: status effects (frozen/reversed/slow) -> CharacterMotor.Simulate -> WeaponHolder.Tick
```

- `ActorCommand` is a plain struct. Netcode only needs to serialize commands and replicate state; the gameplay logic
  doesn't care whether a human, a bot or a remote client produced the input.
- `Movement/CharacterMotor` is a kinematic Quake/Source controller on top of a `CharacterController`. It has ground friction,
  accelerate and air-accelerate (wish speed capped for air strafing), auto-bhop, a jump buffer, coyote time and a crouch-slide boost.
  When it hits a steep surface it clips velocity along the surface instead of stopping, which is what makes surf work. All tunables are in `MovementSettings`.
- `Actor` holds health, `StatusEffects`, death and ragdoll/yeet, respawn, teleport and elimination. `ActorFactory` assembles the
  CharacterController, motor, visual, weapon holder and controller (plus the camera rig for the local player).

## Combat

- `Combat/HitResolver` is the single place hits are applied. `ResolveDirectHit` and `Explode` read the current `GameRules`,
  then scale or skip damage and knockback, apply status effects and raise `ActorHit`. Anything that implements `IDamageable`
  or `IKnockbackable` can be hit: actors, `PhysicsProp`, `TargetDummy`.
- Physics props are real Rigidbodies. Hits add impulses at the hit point, so cans spin, crates tumble and pins topple.
  Explosive barrels chain-explode.

## Weapons

- `WeaponDefinition` is a plain serializable class covering fire mode (semi, auto, burst, charge, beam), hitscan or projectile,
  projectile kind (bullet, bubble, duck, goo, disc, bowling ball, crate...), damage, knockback, spread, pellets, ammo, ricochet,
  pierce and visuals.
- `WeaponModifier`s are stateless. `def.With(mod)` clones the definition and lets the modifier change stats (`Apply`) and add
  impact behaviour (`OnImpact`). Examples: `BubbleProjectiles`, `RubberDuckProjectiles`, `GooSplatter`, `Explosive`, `Homing`,
  `Bouncy`, `KnockbackOnly`, `ShrinkTarget`, `FreezeTarget`, `ReverseControls`, `BigHead`, `LaunchTarget`, `ConfettiOnHit`.
- `WeaponRegistry` builds 69 guns from 22 archetypes plus modifier stacks. Each gun has tags (`dm`, `bowling`, `impair`,
  `surf`, `special`) that the modes use to pick loadouts. `WeaponModelBuilder` builds a primitive gun model from the archetype.
- `WeaponHolder` covers the inventory, switching, reload, the fire queue, charge and beam handling, hitscan with ricochet and
  pierce, spawning projectiles, and the `Concealed` (HR) and `Holstered` (unarmed) flags.

## Presentation

- `Core/Mats` builds Standard-shader materials at runtime and caches them by colour. `ProceduralAudio` synthesizes every
  sound effect with `AudioClip.Create`. `Fx` provides confetti, bursts, tracers, flashes and explosions.
- `Player/PlayerCameraRig` handles the first-person camera with FOV kick at speed, strafe tilt, a recoil spring, landing dip,
  optional head bob and a primitive viewmodel.
- `UI/Hud` provides IMGUI helpers: crosshair and hit marker, speedometer, health and status, weapon panel, kill feed,
  announcements and world labels.

## Compatibility notes

- Input: `Core/GameInput` uses the new Input System when only it is enabled, otherwise legacy `Input`.
- `Core/RbCompat` wraps the APIs Unity 6 renamed (`linearVelocity`, `linearDamping`, `PhysicsMaterial`) behind `UNITY_6000_0_OR_NEWER`.
- The code compiles with no errors or warnings against the Unity 2021.3 reference assemblies (legacy input) and the
  Unity 6 reference assemblies (Input System). It has not been run in the editor yet; see the ROADMAP risks.
