# Cricket UI Figma import

Peer-level uGUI pages at 1080 × 1920:

- `Screen10_511` — existing page retained.
- `Screen10_593` — event matchmaking (`10:593`).
- `Screen63_5` — four-player lobby (`63:5`).
- `Screen10_368` — cricket registry (`10:368`), replacing the placeholder.

Each page has a root prefab, Canvas prefab, scene, and region prefabs under `Prefabs/Parts`. Region roots contain the exact exported Figma visual and transparent named uGUI `Button` children. Buttons have no `onClick` listeners.

Move a region with its `RectTransform`; attach scripts to the page, region, or button. Rebuild via `Tools > Cricket UI > Build Figma Screen Prefabs`.
