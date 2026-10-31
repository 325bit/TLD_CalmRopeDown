# CalmRopeDown — Development Documentation

> Language: English only — code comments, docs/ and ChangeLog.

For maintainers. Player-facing documentation lives in the root `README.md`.

- Mod version **1.3.0** (`MelonInfo` in `src/ModMain.cs`, `<Version>` in `src/CalmRopeDown.csproj`)
- Game **The Long Dark 2.55** (IL2CPP, built against the `stblade.modding.tld.il2cppassemblies.windows 2.55.0` interop set)
- Loader **MelonLoader 0.7.3** (net6), Harmony shipped with the loader
- Dependency **ModSettings.dll** (loader-side framework, resolved from `Mods/` at runtime)
- Every comment that was moved out of the source or the old README is accounted for in §11.

| Companion doc | Content |
|---|---|
| `README.md` | Player-facing: features, install, settings, compatibility |
| `ChangeLog.md` | Per-version history with motivation |

## 1. File Map

| File | Type | Responsibility | Key entry |
|---|---|---|---|
| `src/ModMain.cs` | MelonMod | Assembly attributes; register the settings panel; apply Harmony patches; per-frame tick; restore on quit | `ModMain.OnInitializeMelon` |
| `src/Patches` (in `ClimbTuner.cs`) | Harmony patch classes | Timing only — hook who/when, forward to `ClimbTuner` | §3 |
| `src/ClimbTuner.cs` | internal class | Snapshot / idempotent overwrite / restore of the descent fields; per-frame phase pinning; diagnostics | `Tick`, `PrepareMotion` |
| `src/ModConfig.cs` | JsonModSettings | Settings panel fields and labels; persistence to `Mods/CalmRopeDown.json` | `OnConfirm` |

Naming/layering convention: patch classes stay dumb forwarders ("who, when, forwarded to whom");
all judgement and writes live in `ClimbTuner`; the panel owns nothing but UI state.

## 2. Per-Frame Pipeline

```
MelonMod.OnUpdate
└─ ClimbTuner.Tick
   ├─ disabled → RestoreLive(force:false)          (keep restoring while the toggle is off)
   ├─ GameManager.GetPlayerClimbRopeComponent
   ├─ slot pointer mismatch → BeginSlot             (snapshot vanilla values)
   ├─ ApplyValues                                   (idempotent rewrite, every frame)
   └─ MaybeDebugStateWatch                          (only when DebugLogging)

Game-side events (Harmony):
BeginClimbing  Prefix  → ClimbTuner.EnsureApplied   (overwrite before the climb starts)
UpdatePosition Prefix  → ClimbTuner.PrepareMotion   (pin m_Anim_Phase before motion integrates)
EndClimbing    Postfix → ClimbTuner.RestoreLive     (restore; live instance passed in)
OnApplicationQuit      → ClimbTuner.RestoreLive(force:true)
```

## 3. Injection Points

All targets are `PlayerClimbRope` (game 2.55; all three confirmed present in the interop assembly):

| Target | Patch | Timing | Effect | Notes |
|---|---|---|---|---|
| `PlayerClimbRope.BeginClimbing` | Prefix, `Priority.High` | Before climb state is set up | `EnsureApplied`: snapshot + overwrite | Logs `[Climb] BeginClimbing fired` |
| `PlayerClimbRope.UpdatePosition` | Prefix, `Priority.High` | Every frame, before spline integration | `PrepareMotion`: pin `m_Anim_Phase` | Down state only |
| `PlayerClimbRope.EndClimbing` | Postfix | After the climb ends | `RestoreLive`: restore fields | Logs `[Climb] EndClimbing fired` |

No transpilers, no method-body rewrites. `Harmony.PatchAll(typeof(Patches))` is called explicitly
and the applied method count is logged, so "loaded but not patched" is visible in the log.

## 4. Data and Fields

### 4.1 Game-side fields (PlayerClimbRope, game 2.55)

| Field | Meaning | Unit |
|---|---|---|
| `m_ClimbingState` | `Up=0 / Down=1 / Holding=2 / Falling=3` | enum |
| `m_ClimbSpeedDownFullyRested` | Descent speed input, fully rested end of the lerp | m/s (anim-domain) |
| `m_ClimbSpeedDownExhausted` | Descent speed input, exhausted end of the lerp | m/s (anim-domain) |
| `m_ClimbSpeed` | Speed the game actually integrates each frame | m/s (anim-domain) |
| `m_StaminaDrainPerSecondClimbingDown` | Stamina drain input while descending | /s |
| `m_FatigueDrainPerSecondClimbingDown` | Fatigue drain input while descending | /s |
| `m_Anim_UnitMotion` | Spline metres per unit of anim motion | m/unit |
| `m_Anim_ClimbSpeedToAnimScalar` | Speed-to-anim-phase scalar | – |
| `m_Anim_Phase` | Stride phase, wrapped by the game with `Repeat(x, 2π)` | rad |

### 4.2 Project-side state (`ClimbTuner.Slot`)

| Field | Content |
|---|---|
| `Pointer` | Native instance pointer the snapshot belongs to |
| `SpeedDownRested` / `SpeedDownExhausted` | Vanilla descent speed inputs |
| `StaminaDown` / `FatigueDown` | Vanilla drain inputs |
| `UnitMotion` | Vanilla anim scalar, needed to convert target m/s into field values |

One slot at a time; a pointer mismatch (scene change, prefab rebuild) discards the old slot and
snapshots the new instance.

## 5. Core Model — Constant-Speed Descent

### 5.1 Why field overwrite instead of method patches

`SetClimbSpeed` and `GetPositionOnRope` each contain a copy of the speed calculation, and the copy
inside `GetPositionOnRope` is **inlined in native code** — a Harmony method patch on either is
bypassed by the other path. Every input to those copies, however, is an instance field, so
overwriting fields covers both paths at once.

Native evidence (2.55 disassembly; RVAs are only valid for 2.55):

| Mechanism | Where | Evidence |
|---|---|---|
| Direction | `m_ClimbingState` | `dump.cs` (2.55) |
| Speed | `SetClimbSpeed` @ RVA 0xDDE0B0, `GetPositionOnRope` @ RVA 0xDDC790 | both lerp `m_ClimbSpeedDown{FullyRested,Exhausted}` into `m_ClimbSpeed` |
| Spline advance | `GetPositionOnRope` @ RVA 0xDDCAF9–0xDDCC48 | see §5.2; constants: `Repeat` length 6.2831855 (2π), `Mathf.Sin` @ RVA 0x3FFEB0 |
| Stamina | `UpdateStamina` @ RVA 0xDDDCD0 | picks `m_StaminaDrainPerSecondClimbing{Up,Down,Holding}` by state, linear corrections, ×dt → `PlayerMovement.AddSprintStamina` |
| Fatigue | `UpdateFatigue` @ RVA 0xDDDF10 | same pattern → `Fatigue.AddFatigue` |

### 5.2 Vanilla descent is bursty

Per frame, the rope-spline progress increment is

```
dT = dt × m_ClimbSpeed × m_Anim_UnitMotion × |sin(m_Anim_Phase)| / splineLength
m_Anim_Phase = Repeat(phase + dt × m_ClimbSpeed × m_Anim_ClimbSpeedToAnimScalar, 2π)
```

`|sin(phase)|` pulses 0↔1 twice per stride cycle, so the vanilla descent moves in bursts.

### 5.3 Phase pinning (the constant-speed trick)

The `|sin|` envelope is computed in native code and cannot be removed by fields — but
`m_Anim_Phase` is a writable field that the method reads every frame:

1. The `UpdatePosition` prefix writes `m_Anim_Phase = π/2 − dt·S·A` (S = locked anim-domain speed,
   A = `m_Anim_ClimbSpeedToAnimScalar`), so the in-method update `Repeat(π/2, 2π)` stays π/2 and
   `|sin| = 1` every frame.
2. `ApplyValues` locks both descent speed ends to `S = V / m_Anim_UnitMotion` (V = target line
   speed), so the in-method lerp result is always S regardless of the player's condition.
3. Therefore `dT = dt·(V/U)·U·1/L = dt·V/L` — exactly V m/s, independent of condition and framerate.

Up-climbing never touches `phase` or the Up fields, so it is bit-identical to vanilla.

Side effect: with the phase pinned, the descent animation freezes into a single posture
(a zip-line-like glide) and footstep sounds stop firing. That is inherent to constant speed,
not a bug. Up-climb animation is unaffected.

### 5.4 Why the drain multiplier is exact

The correction chains in `UpdateStamina`/`UpdateFatigue` (sprint stamina correction, difficulty
coefficients, …) are all **linear multipliers of the rate fields**. Multiplying the field by k
multiples the final drain by k — no clamping or non-linear step in between.

## 6. State and Lifecycle

- **On rope**: slot alive, fields overwritten every frame (idempotent).
- **Leave rope** (`EndClimbing`) or **disable toggle**: fields restored, slot kept for re-use? No —
  slot cleared; the next climb snapshots fresh.
- **Scene change / load**: the component is rebuilt from the prefab, so a stale pointer is detected
  by mismatch and the old slot is discarded. Writes are never issued to a destroyed object.
- **Quit** (`OnApplicationQuit`): `RestoreLive(force:true)` clears the slot unconditionally.

## 7. Failure Modes

| Symptom in log | Meaning |
|---|---|
| `[Diagnostic] Tick exception (first one…)` with stack | Runtime lookup failed; repeats are silent by design |
| `[Diagnostic] PrepareMotion exception (first one)` | Phase pin failed this frame; field locking still applies |
| No `[CalmRopeDown] Located PlayerClimbRope…` line | The component was never found — the overwrite never happened |

## 8. Settings

Panel labels are bilingual strings defined verbatim in the `Name`/`Section` attributes of
`ModConfig.cs` and quoted as-is in the README; they are not repeated here so this document
stays English-only.

| Field | Default | Range | Takes effect |
|---|---|---|---|---|
| `Enabled` | `true` | – | immediately on CONFIRM (restore/rewrite) |
| `SpeedMode` | `FullyRested` | choice | immediately on CONFIRM |
| `FixedDownSpeed` | `2.0` | 0.5–12 (47 steps) | immediately on CONFIRM; only in `Fixed` mode |
| `ConsumptionMultiplier` | `0.0` | 0–1 (21 steps) | immediately on CONFIRM |
| `DebugLogging` | `false` | – | next state change |

Persistence: `Mods/CalmRopeDown.json`, written by the ModSettings framework on CONFIRM.

## 9. Log Dictionary

| Prefix | When | How to read |
|---|---|---|
| `[CalmRopeDown] Located PlayerClimbRope and overwrote descent fields` | First successful overwrite | Vanilla baseline vs new values; if absent, nothing was ever overwritten |
| `[Climb] BeginClimbing fired` / `[Climb] EndClimbing fired` | Each rope enter/leave | Confirms the event hooks fire |
| `[State] …` | Debug mode, on state change | `m_ClimbSpeed`, `phase`, effective stamina rate; constant speed ⇒ `m_ClimbSpeed` stable while Down |
| `[Config] confirmed` / `[Config] disabled` | CONFIRM with toggle on/off | Echoes effective values |
| `[Diagnostic] … (first one)` | First exception only | Full stack for Tick; repeats silent by design |
| `Harmony patches applied (N method(s))` | Startup | N must be 3; any other number means a patch class failed to bind |

## 10. Design Constraints

| # | Constraint | If violated | Reference |
|---|---|---|---|
| 1 | Overwrite input fields; never patch `SetClimbSpeed`/`GetPositionOnRope` | The inlined copy inside `GetPositionOnRope` bypasses method patches and speed stays vanilla | §5.1 |
| 2 | Pin `m_Anim_Phase` in the `UpdatePosition` **prefix**, not `MelonMod.OnUpdate` | Ordering against the game's Update is not guaranteed; the pin can land after integration and the descent stays bursty | §2 |
| 3 | Pin only when `m_ClimbingState == Down` | Up/Holding/Falling get a pinned phase — up-climb animation and speed visibly change | `PrepareMotion` |
| 4 | Snapshot vanilla values (`BeginSlot`) before the first write | Vanilla values are lost for the session; disable/quit cannot restore | §4.2 |
| 5 | Restore only while the live pointer matches the slot | Writing into a destroyed native object crashes on scene unload | `RestoreLive` |
| 6 | Guard `UnitMotion > 1e-5` before dividing | Division by zero / NaN speed on degenerate data; fallback is field locking only (constant speed not guaranteed) | `PrepareMotion` |
| 7 | Lock **both** descent speed ends to `V/UnitMotion` | A single-end lock leaves the lerp condition-dependent; speed drifts as stamina changes | `ApplyValues` |
| 8 | First apply and first exception are logged unconditionally, then silent | Either log spam every frame, or a silent failure nobody can diagnose | §9 |
| 9 | Touch only the four Down fields + `m_Anim_Phase`; never write Up/Holding/Falling values or the save file | Up-climb changes; or save-schema contamination | §5.3 |

Source code carries the corresponding `§10 #N` marker at each constrained site.

## 11. Comment Migration Map

From the pre-1.3.0 Chinese README ((mechanism), (verification), (compatibility) sections) and the original
Chinese comments in `src/*.cs`:

| Original location | Original content | Now at |
|---|---|---|
| old README old-README mechanism table | direction/speed/stamina/fatigue mechanism table with RVAs | §5.1 |
| old README old-README mechanism | "why overwrite fields, not patch methods" (inlined copy) | §5.1 |
| old README old-README mechanism | vanilla T-increment formula and |sin| envelope | §5.2 |
| old README old-README mechanism | phase-pinning derivation (v1.2.0) | §5.3 |
| old README old-README mechanism | zip-line posture visual note | §5.3 side effect; also README FAQ |
| old README old-README mechanism | drain multiplier exactness argument | §5.4 |
| old README old-README verification | verification steps (up-climb parity, constant descent, multiplier ratio, rollback) | §14 |
| old README old-README compatibility | compatibility boundaries | README Compatibility; §3 notes |
| old README old-README structure | project structure tree | §1 |
| `ModMain.cs` class summary | mechanism positioning (state enum, speed paths, drain fields) | §4, §5.1 |
| `ModMain.cs` class summary | overwrite strategy + snapshot/restore + no save writes | header summary; §10 #9 |
| `ClimbTuner.cs` class summary | vanilla formula + constant-speed derivation | §5.2, §5.3 |
| `Patches` class summary | three hook points and timing | §3 |
| `RestoreLive` doc comment | force semantics, destroyed-object guard | §6, §10 #5 |
| `PrepareMotion` doc comment | why Harmony prefix instead of OnUpdate | §10 #2 |
| `ApplyValues` inline comments | lock both ends / linear drain chain | §10 #7, §5.4 |

## 12. Maintenance Duties

| Changed | Must sync |
|---|---|
| Injection points (who/when) | §3 table + patch-class comments |
| Settings fields/defaults/ranges | §8 table + README Settings |
| New or changed log lines | §9 dictionary |
| Gates, write order, overwrite targets | §10 table + linked sections |
| Files added/removed or responsibilities | §1 file map |
| Any narrative removed from comments | §11 migration map |
| Player-visible behaviour | ChangeLog.md + README.md |

Version numbers live in exactly three places: `src/ModMain.cs` (`MelonInfo`), `src/CalmRopeDown.csproj`
(`<Version>`), and the ChangeLog first entry.

## 13. Build and Deploy

```bash
cd src
dotnet build -c Release
```

- Output: `src/build/CalmRopeDown.dll`.
- The csproj's `DeployToGame` target copies the dll to `$(GameDir)\Mods\` after every build.
  `GameDir` in the csproj points at the maintainer's install
  (`F:\GameAbouts\The Long Dark\TheLongDark_2.55`) — adjust before building elsewhere.
- References are resolved from the game folder plus the local NuGet cache
  (`stblade.modding.tld.il2cppassemblies.windows 2.55.0`); `ModSettings.dll` comes from `Mods/`.
- Comment-only changes must still compile clean: a wrong `<see cref>` fails with CS1574.

## 14. Verification Status

| Item | Status |
|---|---|
| Compile | Release build clean (zero warnings/errors) |
| Injection points | Applied — startup logs `Harmony patches applied (3 method(s))` |
| In game | Verified on 2.55 / ML 0.7.3: constant descent (stable `m_ClimbSpeed` in `[State]` logs), up-climb parity vs mod-disabled runs, drain ratio matches multiplier, disabling mid-climb restores vanilla behaviour immediately |
| Numbers | Vanilla baselines are logged once at first overwrite and were cross-checked against the disassembly values |
