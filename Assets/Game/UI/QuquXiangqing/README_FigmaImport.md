# 蛐蛐详情页 Figma 导入

来源：Figma 文件 `5jALrfLrmLV0NIOxaQURCu`，节点 `10:527`（details-popup-card）。

- 设计尺寸：972 × 1336
- 页面 Prefab：`Prefabs/CricketDetail.prefab`（根上带 Canvas + `QuquXiangqingView`）
- 模块 Prefab：`Prefabs/Parts/*.prefab`
- 场景：`Assets/Scenes/ququxiangqing.unity`
- Figma 导出图：`Textures/Figma/QuquXiangqing_10_527.png`
- 原始蛐蛐素材：`Textures/Figma/VioletCricketIllustration.png`

页面不依赖整页 PNG。属性卡共用一份 `Parts/StatCard.prefab`，里面的 Label / Value 是普通子物体。除售卖 / 收入背包 / 关闭三个按钮外，其余区域也是普通子物体。三个操作按钮挂有 `QuquXiangqingView` 的调试日志回调，便于后续接入业务逻辑。

在 Unity 菜单执行 `Tools > Cricket UI > Rebuild Ququ Detail (Figma 10:527, Modular)` 可重新生成 Prefab 和场景。
