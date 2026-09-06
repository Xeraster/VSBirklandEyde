
---
type: reference
title: Block particles and turning them on and off
description: JSON particleProperties spawn from an off-thread scan of nearby blocks; ShouldReceiveClientParticleTicks is the registration gate and OnAsyncClientParticleTick is the per-tick one.
tags: [blocks, particles, client, threading]
vs_version: "1.22.6"
verified: 2026-09-03
sources:
  - VintagestoryAPI.dll Vintagestory.API.Common.Block.ShouldReceiveClientParticleTicks / OnAsyncClientParticleTick
  - VintagestoryAPI.dll Vintagestory.API.Common.BlockBehavior.ShouldReceiveClientParticleTicks
  - VintagestoryAPI.dll Vintagestory.API.Common.CollectibleObject.ParticleProperties
  - VintagestoryAPI.dll Vintagestory.API.Common.AdvancedParticleProperties
  - VintagestoryLib.dll Vintagestory.Client.NoObf.SystemClientTickingBlocks
  - VintagestoryLib.dll Vintagestory.Client.NoObf.SystemRenderParticles.OnSeperateThreadGameTick
  - VintagestoryLib.dll Vintagestory.Client.NoObf.ParticleManager, ParticlePoolQuads.SpawnParticles
  - VintagestoryLib.dll Vintagestory.Client.NoObf.ClientThread.Process
  - VSSurvivalMod.dll Vintagestory.GameContent.BlockBehaviorNoParticles, BlockFruitTreePart, BlockSeaweed
  - VSEssentials.dll Vintagestory.GameContent.BlockEntityParticleEmitter
---

A blocktype's `particleProperties` array needs no code. `CollectibleObject.ParticleProperties`
(`AdvancedParticleProperties[]`) is populated straight from the JSON, and the base
`Block.OnAsyncClientParticleTick` spawns every entry in it. Nothing else is involved — there is no
particle registry, no emitter object, and no block entity.

Turning that on and off means intervening at one of two gates, and they are not equivalent.

## The two gates

```csharp
// gate 1 — is this position registered as a particle ticker at all?
public virtual bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player,
                                                     BlockPos pos, out bool isWindAffected)

// gate 2 — called ~30x/second for every registered position
public virtual void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos,
                                              float windAffectednessAtPos, float secondsTicking)
```

`ShouldReceiveClientParticleTicks` is the **registration** gate. Its base implementation returns
`true` iff `ParticleProperties` is non-empty, which is exactly why JSON alone is enough. It is
evaluated only during a scan (below), so a `false` returned from it takes effect **whenever the
next scan happens — up to 20 seconds later**, not immediately.

`OnAsyncClientParticleTick` is the **per-tick** gate, and it is the responsive one. Overriding it
and conditionally calling `base` (or not) switches particles at the next 33 ms tick.

Use both: the cheap registration gate to keep positions that can never emit out of the ticker
dictionary, and the per-tick gate for state that changes while the player is standing there.

```csharp
public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos,
                                               float windAffectednessAtPos, float secondsTicking)
{
    if (!EmittingAt(manager.BlockAccess, pos)) return;      // off — spawn nothing
    base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
}
```

Vanilla's own hard-off switch is a behaviour, `BlockBehaviorNoParticles`, whose whole body is
`handling = EnumHandling.PreventSubsequent; return false;`. Adding `{ "name": "NoParticles" }` to a
blocktype is the no-code way to suppress inherited particles.

Behaviours see the gate with `ref EnumHandling handling` instead of a return-only signature.
`Block.ShouldReceiveClientParticleTicks` ORs together every behaviour that set `handling` to
anything but `PassThrough`, returns early on `PreventSubsequent`, and falls through to the
`ParticleProperties`-non-empty default **only if no behaviour claimed it**. So one behaviour
returning `false` with `PreventDefault` is enough to kill JSON particles.

## Why an override may never be called

The dispatch is `manager.BlockAccess.GetBlock(pos)?.OnAsyncClientParticleTick(...)` — a virtual call
on the block-registry singleton for that position. If particles still spawn but the override does
not run, the block at that position is not an instance of the class, i.e. the class is not attached:

- `"class"` missing from the blocktype JSON, or not matching the `RegisterBlockClass` name;
- registered in `StartClientSide` only, or in `StartPre` — it belongs in `Start(ICoreAPI)` on both
  sides, see [class registry](class-registry-and-patching.md);
- the build was not redeployed. `OnLoaded` with a `VerboseDebug` line settles this in one launch.

Two further ways to see no call at all:

- **The block is not in the default block layer.** Both the scan (`chunk.Data[...]`) and the tick
  (`GetBlock(pos)`, `BlockLayersAccess.Default`) read the solid layer. A block living in the fluid
  layer is never registered and never ticked.
- **The scan has not reached it yet.** See below.

## When registration actually happens

`SystemClientTickingBlocks` owns the whole mechanism, client-side, and rescans on three triggers:

- the player leaves their current 8-block grid cell (`EnumProperty.PlayerPosDiv8`),
- a 20-second timer, and
- 1 second after block textures load.

A scan walks a **37-block radius** cube around the player (`scanRange = 37`, despite the API doc
comment saying 32), 11,000 positions per 5 ms thread tick, calling
`ShouldReceiveClientParticleTicks` on each and collecting the hits into `currentTickers`. Only when
the whole cube is done does `CommitScan` swap them in on the main thread. A large scan therefore
takes several seconds to take effect — which is what the API's "takes a few seconds for the game to
register the block" is referring to.

`OnBlockChanged` is the exception: placing or changing a single block registers that one position
immediately, without waiting for a scan. Removal is not handled symmetrically — a position stays in
the dictionary until the next commit, and the tick simply calls whatever block is there now.

The client command `.ctblocks true` freezes the whole set, which is useful when a scan keeps
clearing state you are trying to observe.

## It runs off the main thread, ~30 times a second

The ticker is registered via `IClientEventAPI.RegisterAsyncParticleSpawner`, and all such spawners
are driven from `SystemRenderParticles.OnSeperateThreadGameTick` on a **separate client thread**, in
a fixed `0.033f` accumulator loop — so 33 ms steps, and after a stall it may run the loop several
times back to back (up to 1 second's worth). The `Block` doc comment saying "every 25ms" is wrong.
Ticking stops entirely while `game.IsPaused`.

Three consequences:

- **A throw here kills the game.** `SystemClientTickingBlocks` catches and rethrows, and
  `ClientThread.Process` treats an escaped exception as fatal: log `Fatal`, a "Client Thread Crash"
  message box, and `KillNextFrame`. Off-thread work must not touch main-thread-only state, and a
  null block entity must be handled, not assumed.
- **Use `manager.BlockAccess`, not `api.World.BlockAccessor`.** It is the accessor the caller
  prepared for this thread (an `ICachingBlockAccessor` it calls `Begin()` on each tick).
- Returning `false` from your own spawner delegate unregisters it — that is how a mod-owned async
  spawner turns itself off for good.

## The properties objects are shared per blocktype

`Block.ParticleProperties[i]` is one object per blocktype, not per position: the base tick writes
`basePos` and `WindAffectednesAtPos` into that shared instance immediately before `Spawn`. Mutating
it (colour, quantity, velocity) is fine inside the tick, because every block's tick runs
sequentially on the one particle thread and the spawn reads the values immediately — but the change
is global to the blocktype and persists into the next position's tick unless it is written every
time. Per-position variation means spawning your own `AdvancedParticleProperties` /
`SimpleParticleProperties`, not editing the block's.

`Quantity`, `Size`, `LifeLength`, `GravityEffect`, `Velocity` and `HsvaColor` are `NatFloat`s, so a
per-tick `props.Quantity = NatFloat.createUniform(0f, 0f)` is a valid soft "off" — though skipping
the `Spawn` call is cheaper and unambiguous.

## The client's Particle Level can drop the spawn silently

`ParticlePool*.SpawnParticles` refuses a spawn when
`!props.IgnoreUserConfig && QuantityAlive * 100 >= game.particleLevel * poolSize`. At Particle Level
0 nothing spawns at all, and at low settings a busy scene drops spawns with no log line. Set
`IgnoreUserConfig` only for something the mod cannot function without — it is opting out of the
player's performance setting.

## The main-thread alternative

`BlockEntityParticleEmitter` (the creative-mode emitter block) does the same job from a 25 ms
`RegisterGameTickListener` on the block entity, spawning `block.ParticleProperties` through
`World.SpawnParticles` after an `InRangeOf(pos, 16384f)` — 128 block — check. It is the pattern to
copy when emission depends on block entity state that is awkward to read off-thread, at the cost of
running on the main thread and needing its own distance culling.

`BlockEntity` itself has **no** particle hook; the virtuals exist on `Block` and `BlockBehavior`
only.