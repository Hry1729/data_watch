# Blue editorial landing — design QA

Source: C:/Users/myrh/.codex/visualizations/2026/09/05/01a06f4a-2a7a-79d2-8b7f-0993f733fa90/blue-style-final-set/ (11 approved images).
Implementation: http://localhost:5173/landing#/landing
Evidence: frontend/design-references/blue-implementation/00.png through 10.png; overview.jpg; comparison.jpg; agent-comparison.jpg; mobile-agent.png.
Viewport: desktop 1505 × 1045 CSS pixels, device scale factor 1. Source frames normalized to the same aspect ratio for comparison. Contact sheets are reduced evidence; individual captures retain original render resolution. Mobile: 390 × 844 CSS pixels.
State: authenticated, 11 sections individually scrolled into view, fonts loaded, onboarding notifications dismissed by expiry.

## Findings and history
- Initial P1: deployment headline exceeded right edge. Reduced headline width and preserved display height; subsequent capture contains the full sentence.
- Initial P2: contract numeral crowded schema caption. Reduced numeral height; caption now occupies the bottom of the yellow column.
- Initial P2: Collector label was obscured by Agent. Reduced Latin display size and adjusted module positions; focused agent-comparison.jpg records the revised state.
- P3: generated source uses more compressed Chinese lettering, more vivid yellow particle strands, and different photo color grading. Heavy Noto Sans SC subset and Anton reproduce the type hierarchy, but this is not pixel-identical artwork.

## Required fidelity surfaces
- Typography: local Anton display face and Chinese weight 900 subset; header, titles, outlined words, module names, captions reviewed. No clipped primary headlines in latest desktop captures.
- Layout: 11 independent compositions retained, including staggered steps, architecture bands, central Agent, photo split, case metrics and white closing panel. Mobile Agent composition checked; 390px container scrollWidth equals clientWidth.
- Colors: cobalt, acid yellow, white and black retained. Source raster texture/color differences remain as P3.
- Images: existing dedicated ribbon, port and server raster assets used; no complete page screenshot is used as the implementation. Grain and strand details differ from the reference.
- Copy: approved module variant and approximately 100ms metric retained. Product interface panels explicitly marked as illustrative.

## Validation
- vue-tsc --noEmit: passed.
- Vue template compilation: passed.
- Browser page errors: none.
- Case navigation: selected state confirmed after click and scroll.
- All 11 section screenshots captured successfully.
- Main and document links point to existing routes; destination business workflows are outside this visual change and were not retested.

## Follow-up polish
- P3 source texture, display-font compression and image grading fidelity.
- Wider mobile interaction coverage beyond the checked Agent screen remains a test gap.

final result: passed

## Responsive correction — 2026-09-07

Root cause: width-driven type and height-driven positioning were stretched independently. The earlier 1505 × 1045 comparison did not validate 16:9 and missed intrinsic heading/content collisions.

Changes: index.vue wraps each composition in dw-canvas; blue-editorial.scss fits that canvas to both available dimensions at its approved 1505:1045 ratio, preserves the mobile rules, and reserves additional headline clearance. Wide windows gain side space; taller desktop windows gain vertical space. Intentional decorative clipping and Agent panel overlaps remain.

Evidence: frontend/design-references/responsive-before/ and responsive-after/. Each contains 11 screenshots at each of 1920×1080, 1366×650, 1024×768, 2560×1080, and 390×844; same CSS viewport and deviceScaleFactor 1. responsive-after/comparison.jpg is the combined before/after full-view comparison. Focused geometry checks cover masthead/copy, title/pain block, title/journey caption, contract title/funnel, module heading/subtitle, deployment title/photos, interface title/security, case title/marker, roadmap title/copy, and closing title/actions.

History:
- P1: 16:9 title/content overlap, architecture spine overflow, closing title clipping. Fixed by coupled width/height fitting and headline clearance.
- P2: final geometry pass found cover copy overlap and subpixel case marker overlap. Added explicit vertical clearance and reran the full checks.
- Post-fix: no unexpected primary-region boundary violations in 55 captures; no negative clearances across the ten critical region pairs at seven viewport sizes (also 1280×720 and 800×600); case navigation selected state passes at all seven sizes; browser page errors empty. See responsive-after/checks.json and layout-checks.json.
- Fonts, image assets, copy, and brand colors preserved; display heading sizes adjusted where needed to prevent collisions. Desktop canvas gutters are intentional. Mobile composition checked at 390×844.
- vue-tsc --noEmit passed. The user's running server was used without starting or stopping it.

final result: passed

## Full-width responsive correction — 2026-09-07

User rejected the fixed-ratio canvas gutters. Replaced the fixed 1505:1045 fit with a 100% width/height stage. Regions now span the available viewport; type sizes independently use the smaller width/height scale. The Agent circle is height-bounded and centered, and the hero masthead uses aspect-ratio breakpoints to span the width without colliding vertically with its copy. This supersedes the fixed-canvas decision above.

Evidence: frontend/design-references/responsive-fullwidth/ contains 55 screenshots (11 pages at 1920×1080, 1366×650, 1024×768, 2560×1080 and 390×844), plus ratio-comparison.jpg showing full-width 16:9 and 4:3 together. The page canvas width equals viewport width within 2px in all captures. Existing raster assets, fonts, content and color palette remain in use.

Iteration: initial masthead transform was unsupported by the local browser and allowed overlap at wide ratios. Replaced it with compatible aspect-ratio breakpoints. Post-fix layout-checks.json has no negative headline/content clearances across 7 viewport sizes and no navigation failures. checks.json has no primary-region clipping or page errors. vue-tsc --noEmit passed. User's running server was not started or stopped.

final result: passed
