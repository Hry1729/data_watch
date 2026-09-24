# Design QA — 电影感监控大屏

- Source of truth: `frontend/design-references/monitor-screen-cinematic-target.png`
- Implementation: `/monitor-screen-cinematic`
- Reference viewport: 1857 × 847
- Validation viewport: unavailable

## Automated checks

- `vue-tsc --noEmit`: passed
- `vite build`: passed
- Original screen `src/views/monitor-screen/index.vue`: unchanged
- Diff whitespace check: passed (Git only reports the repository's LF → CRLF conversion notice)

## Visual comparison

Status: blocked before screenshot capture.

The in-app browser runtime cannot start on this Windows host because its sandbox cannot decrypt the local browser credential (`CryptUnprotectData`, code `2148073483`). As a result, no browser-rendered implementation screenshot or composite reference-versus-implementation comparison could be produced in this run.

### Iteration after user comparison

The first implementation was rejected for visible frame and transparency mismatch. This revision applies:

- the original dashboard's industrial background image and overlay alpha values;
- the original dashboard's 52% → 42% translucent panel treatment with blur and saturation;
- segmented chamfered panel frames with recessed top rails and interrupted bottom rails;
- a clipped title tray for every data panel;
- a recessed command-title frame with a centered lower notch.

### Current iteration

The user requested a stronger unified overlay, a denser top KPI deck, balanced content-row heights, a bottom-row trend layout, narrower edge panels, and a calmer center device-online animation. This revision applies:

- one page-level `rgb(0 11 36 / 85%)` mask above the background image;
- fully transparent KPI cards, data panels, and driver cells;
- twelve compact KPI cards matching the original dashboard's metric set, with the KPI row reduced from `92px` to `72px`;
- a `2fr / 1fr` split between the two-row main region and the one-row operations region, making all three visible card rows approximately equal height;
- ring-chart center labels moved from `35%` to `38%` so their visual baseline sits lower;
- matching three-column ratios for the main content and bottom row: `0.92fr / 2.35fr / 0.92fr`;
- recent alerts in the narrow left column, collection/alarm trend in the wide center column, and driver availability in the narrow right column;
- a compact four-field recent-alert row and a vertical five-slot driver status layout for the narrower edge panels;
- a simplified device-online reactor with two segmented shells, one live progress ring, one restrained scan sector, and two orbiting particles;
- a larger, better-centered online-rate core using the full middle content area.

The full `pnpm build` passes. A fresh in-app browser capture was attempted after the build, but the Windows browser sandbox still exits with `CryptUnprotectData` code `2148073483`.

The implementation is therefore not marked as visually passed. Final sign-off still requires a rendered screenshot at the reference aspect ratio and verification of:

1. frame geometry and the recessed title shapes against the selected concept image;
2. background visibility and card translucency against the original dashboard;
3. the animated center device-online reactor;
4. the version-3 recent-alert table treatment;
5. the version-2 five-slot driver-availability panel;
6. full-screen spacing, clipping, and motion behavior.

## Final result

blocked

Blocker: browser-rendered implementation evidence is unavailable because the in-app browser sandbox cannot start on this Windows host. Code compilation and production build pass.
