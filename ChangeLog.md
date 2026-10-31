# ChangeLog

All notable changes to CalmRopeDown are documented here. Format: version — headline,
motivation, changes, behaviour impact.

## 1.3.0 — 2026-10-31

First public release. No behaviour change since 1.2.0; this release is the open-source
alignment pass.

- Motivation: bring the repository up to the shared mod conventions (English code comments,
  English development docs, bilingual player-facing README) before publishing.
- Changed:
  - Source comments and runtime log messages rewritten in English; log lines are now
    `[CalmRopeDown]`, `[Climb]`, `[State]`, `[Config]`, `[Diagnostic]`.
  - `docs/DEVELOPMENT.md` added; all implementation detail (RVAs, formulas, constraints,
    verification) moved out of the README into it.
  - `README.md` rewritten as a bilingual player-facing document; the old developer-oriented
    Chinese README is superseded.
  - Author field corrected to `Hemalurgic` (`MelonInfo`).
  - Startup log now reports the number of applied Harmony patch methods (must be 3).

## 1.2.0 — Constant-speed descent (phase pinning)

- Motivation: locking the speed fields alone still produced a bursty descent because the
  vanilla spline advance carries a |sin(phase)| envelope computed in native code, which no
  field can disable.
- Changed: the `UpdatePosition` prefix now pins `m_Anim_Phase` to π/2 − dt·S·A every frame
  while descending, holding the envelope at its peak; combined with the speed-field lock the
  descent advances at exactly the target line speed (see `docs/DEVELOPMENT.md` §5.3).
- Behaviour impact: descending is now strictly constant-speed regardless of condition and
  framerate. The descent animation freezes into a single posture (zip-line-like glide) and
  footstep sounds stop while descending — inherent to constant speed, not a defect.
  Up-climbing is untouched.

## 1.1.0 — In-game settings

- Motivation: expose the tuning knobs without editing files.
- Changed: ModSettings panel (toggle, speed mode, fixed speed slider, drain multiplier
  slider, debug logging) with persistence to `Mods/CalmRopeDown.json`; CONFIRM applies
  immediately.
- Behaviour impact: disabling the toggle or unmounting the mod restores vanilla behaviour
  at once, no restart required.

## 1.0.0 — Initial implementation

- Constant-rate descent for climbing ropes via overwriting the four "down" input fields of
  the live `PlayerClimbRope` instance (vanilla values snapshotted and restored), plus a
  stamina/fatigue drain multiplier applied to the two descent drain fields. Up-climbing,
  holding and falling are untouched; no save data is written.
