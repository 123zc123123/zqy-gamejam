# Cricket UI Figma import

Peer-level uGUI pages at 1080 × 1920:

- `Screen10_593` — event matchmaking (`10:593`).
- `Screen63_5` — four-player lobby (`63:5`).

图鉴页改用 `Resources/Collection/Prefabs/FigmaImport_cricket-collection_10_6`，不再重建 368 / 511。

Each page has a root prefab, Canvas prefab, scene, and region prefabs under `Prefabs/Parts`. Region roots contain the exact exported Figma visual and transparent named uGUI `Button` children. Buttons have no `onClick` listeners.

Move a region with its `RectTransform`; attach scripts to the page, region, or button. Rebuild via `Tools > Cricket UI > Build Figma Screen Prefabs`.
