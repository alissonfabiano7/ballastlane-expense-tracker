# ADR-004 — Angular 21 (zone-based, Reactive Forms) over 22 + zoneless

**Status:** Accepted (Sprint 2)

## Context

The original planning artifact targeted Angular 22 + Signal Forms +
zone-based. By the time the frontend was scaffolded (May 2026), Angular
22 was still in pre-release (`22.0.0-next.7`) — not a defendable choice
for a take-home where the panel will run a clone-fresh setup. Both
Angular 21 (stable) and Signal Forms maturity needed reconsideration.

## Decision

**Angular 21.2.x stable, zone-based change detection, Reactive Forms**,
with standalone components, `ChangeDetectionStrategy.OnPush`, and signals
for component-local state.

- **Angular 21 over 22:** 22 was pre-release; pinning to a stable major
  is mandatory for a take-home.
- **Zone-based over zoneless:** an early experiment with
  `provideZonelessChangeDetection()` produced flaky behavior in Angular
  Material's CDK overlays (`mat-select`, `mat-datepicker`). Reverted to
  zone-based to keep the UI predictable; OnPush + signals already give
  the perf benefit that matters for this scope.
- **Reactive Forms over Signal Forms:** Signal Forms is still maturing
  in 21; Reactive Forms is the well-trodden path for the four screens
  this app needs.

## Consequences

**Positive**

- Stable major + stable change detection model = no surprises during the
  panel demo.
- OnPush + signals is already the modern Angular pattern; missing
  zoneless doesn't read as "behind", it reads as "considered and
  rejected for stability".
- Reactive Forms have the deepest integration with Material (validators,
  `mat-error`, custom matchers).

**Negative**

- Doesn't show off zoneless or Signal Forms — both are the future. With
  more runway I'd revisit zoneless after auditing every Material
  component used.
- The take-home documentation (planning artifact) had to be amended
  honestly to reflect the 21 choice, with the original intent preserved
  for narrative.

## Alternatives considered

- **Angular 22 next** — pre-release; not defendable.
- **Angular 20** — still supported but trailing; no reason to reach
  back.
- **Zoneless on 21 with Material** — broken overlay behavior observed
  during Sprint 2 experiment.
- **Signal Forms on 21** — usable but less battle-tested with Material.

## Notes

Documented in a Human-discovered card under "issues caught but not
deep-dived" (`provideZonelessChangeDetection` reverted in `9e8a898`).
The plan-vs-shipped delta is part of the audit trail's authenticity.
