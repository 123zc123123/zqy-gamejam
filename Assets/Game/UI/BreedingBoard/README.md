# 育虫盘美术预制体

正式玩法场景用 **`Assets/Scenes/Merge.unity`**，里面放的是本目录的页面预制体，由 `DouQuquBreedingBoardView` 绑格子和按钮。

美术预览场景是 `Scenes/BreedingBoard.unity`，和正式场景用同一份页面，只用来对布局。

设计分辨率 **1080 × 1920 竖屏**。

## 场景里拖哪一个

| 文件 | 实际是什么 |
| --- | --- |
| `Prefabs/BreedingBoard.prefab` | **页面壳**。场景里拖这个。里面嵌着 Canvas。 |
| `Prefabs/BreedingBoardCanvas.prefab` | **真正的 Canvas**（缩放、底图、标题、棋盘、底栏都在这）。不要单独再往正式场景拖一份，会叠两层。 |

零件都在 `Prefabs/Parts/`，已经被 Canvas 嵌好，一般不用单独拖进场景。

## 页面结构（从上到下）

```
BreedingBoard                  页面壳 1080×1920
└── BreedingBoardCanvas        Overlay Canvas
    ├── 育虫盘底图1            全屏底图
    ├── EventTitlePanel        顶栏「育虫盘」标题
    ├── GoldDisplay            右上金币
    ├── MainEventActions       标题下方一排操作
    │   ├── RulesButton        玩法规则
    │   ├── ArenaRing          中间金圈
    │   ├── ArenaStatus        圈上数字（现显示 99）
    │   └── BackpackButton     背包（当前 = 放一枚卵）
    ├── 棋盘 Board             4×5 共 20 格
    │   ├── 棋盘底 BoardBase
    │   └── Cell 1 … Cell 20   同一份格子预制体的 20 个实例，左上到右下，行优先
    └── BottomEventCarousel    底栏（返回 + 可横滑的六个功能入口）
        ├── BackIcon           返回主界面
        └── TabViewport        RectMask2D 蒙版，宽度 = 原先三个页签总宽 780
            └── TabContent     横滑内容
                ├── BattleTab / BreedingTab / RegistryTab
                └── RankingTab / ShopTab / AcademyTab
```

格子编号：`Cell 1` 左上，`Cell 4` 右上，`Cell 17` 左下，`Cell 20` 右下。程序按这个编号对 `DouQuquMergeBoard` 的 0–19。场景对象名带空格，共用 `Prefabs/Parts/Cell.prefab`。

点中间金圈（规则和背包之间）会在空格放一只幼虫。同级拖到一起合成：幼虫 → 中虫 → 成虫 → 精品虫。

成虫合成精品时抽出品质和性格，棋子留在盘上：

| 品质 | 概率 | 说明 |
| --- | --- | --- |
| 凡品 | 50% | 占位着色 |
| 灵品 | 28% | 占位着色 |
| 仙品 | 17% | 占位着色 |
| 极品 | 5% | 保底；立绘占位为吕布 / 貂蝉 / 诸葛亮 / 关羽 |

性格四选一（各 25%）：猛攻（吕布）、灵巧（貂蝉）、智控（诸葛亮）、稳重（关羽）。凡品到仙品只显示性格名，极品显示对应三国名。性格会改战斗里蓄力、体重、摩擦和角度的配比。

## 零件文件对照

文件名是 Figma 导入时留下的英文，和界面上的中文对不上。用途如下。

### 棋盘

| 文件 | 界面上是 |
| --- | --- |
| `BreedingBoard_Board.prefab` | 整块棋盘（含 20 格） |
| `BreedingBoard_BoardBase.prefab` | 棋盘米色底板 |
| `Cell.prefab` | 单个格子，带 Button，给拖合成用。棋盘上 20 格都是这份的实例 |

### 顶栏 / 操作

| 文件 | 界面上是 |
| --- | --- |
| `BreedingBoard_EventTitlePanel.prefab` | 「育虫盘」标题条 |
| `BreedingBoard_GoldDisplay.prefab` | 金币数字 |
| `BreedingBoard_MainEventActions.prefab` | 规则 + 金圈 + 背包 这一整排 |
| `BreedingBoard_RulesButton.prefab` | 玩法规则 |
| `BreedingBoard_BackpackButton.prefab` | 背包 |
| `BreedingBoard_ArenaRing.prefab` | 中间金圈底 |
| `BreedingBoard_ArenaStatus.prefab` | 金圈上的数量 |

### 底栏

| 文件 | 界面上是 |
| --- | --- |
| `BreedingBoard_BottomEventCarousel.prefab` | 底部整条导航：返回固定，右侧六个入口可横滑 |
| `BreedingBoard_BackIcon.prefab` | 返回 |
| `BottomNavTab.prefab` | 六个功能入口共用这一份。实例只改名字、显示文案和 icon 路径 |

## 贴图

常用的在 `Textures/Figma/`：

- `BreedingBoard_Background_108_93.png` 全屏底
- `BreedingBoard_Coins.png` 金币图标
- `BackCircle.png` / `BackIcon.png` 返回
- `Ellipse2.png` / `Ellipse3.png` 金圈

同名 `.svg` 是导出残留，运行时用 PNG。

## 不要随便点菜单重建

`Tools > Cricket UI > Rebuild Breeding Board` 会按 Figma 坐标重写零件。布局已经按预览场景调过，重建会把场景覆盖冲掉。改外观请直接改对应零件预制体。
