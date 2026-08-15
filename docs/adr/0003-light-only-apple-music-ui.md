# ADR 0003: Light-only Apple Music UI

Status: Accepted
Date: 2026-08-15

## Context

The locked product direction requires an Apple Music-inspired visual language and first-class motion quality. The earlier theme decision included both light and dark appearances. The user has now explicitly narrowed the implementation to light appearance only so effort can remain focused on fidelity, spatial continuity, and interaction quality.

## Decision

1. AlbumRobot Web implements only a light appearance. It does not switch with `prefers-color-scheme: dark` and does not carry dark-theme tokens or dark component variants.
2. The Apple Music-inspired hierarchy remains unchanged: artwork-first Album Grid, Large Title to Compact Title, translucent material navigation, restrained typography, generous whitespace, and red as a controlled accent.
3. Motion remains a product-quality requirement: micro, UI, and spatial layers; responsive and soft spring classes; interruptible Shared Element and Sheet transitions; subtle overshoot only; and stable browser-back semantics.
4. `prefers-reduced-motion` remains supported. It reduces large spatial movement and strong spring behavior without introducing a dark appearance.
5. Mobile uses a material Bottom Tab Bar and expanded Bottom Sheet. Wide desktop uses a light Navigation Rail and Floating Detail Sheet while sharing the same content and state model.

## Consequences

- Dark-mode CSS and QA are removed from the V1 scope.
- Saved effort is spent on light-material fidelity, responsive layout, motion continuity, touch behavior, and real-device QA.
- Future dark appearance work requires an explicit product decision and a separate token/visual QA pass; it must not be reintroduced opportunistically.
