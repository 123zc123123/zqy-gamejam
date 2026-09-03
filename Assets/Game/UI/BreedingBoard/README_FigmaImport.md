# 育虫盘 Figma 导入

来源：Figma 文件 `5jALrfLrmLV0NIOxaQURCu`，节点 `91:8`（育虫棋盘）。

- 设计分辨率：1080 × 1920
- 页面 Prefab：`Prefabs/育虫盘.prefab`
- Canvas Prefab：`Prefabs/Canvas.prefab`
- 组件 Prefab：`Prefabs/Parts/*.prefab`（EventTitlePanel、MainEventActions、Board、Cell1~Cell20、底部导航等）
- 场景：`Scenes/BreedingBoard.unity`
- 视觉导出：`Textures/Figma/BreedingBoard_91_8.png`

`RulesButton`、`BackIcon`、底部导航和 `Cell1`~`Cell20` 等组件均为独立 Prefab，可直接挂载业务脚本。

在 Unity 菜单执行 `Tools > Cricket UI > Rebuild Breeding Board (Figma 91:8, Modular)` 可重新生成页面资源。
