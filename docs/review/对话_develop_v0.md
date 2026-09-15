# 斗蛐蛐 · 对话框

> 从 DeadZone DialogBoxView 搬过来的最小对白：组、逐句打字机、点击揭完/下一句。不建立绘、电话框、选项、StoryLine。
> 其他 AI 增改对白只改 `Assets/Resources/Dialogue/*.txt`，不要改 C#。

1. [怎么播](#1-怎么播)
2. [怎么写一份对白](#2-怎么写一份对白)
3. [点击与打字机](#3-点击与打字机)
4. [不要搬什么](#4-不要搬什么)

---

## 1. 怎么播

```csharp
DialogueBoxView.Play("dlg.sample.box", () => {
    // 整组结束
});

bool playing = DialogueBoxView.IsPlaying;
bool typing = DialogueBoxView.IsTyping;
DialogueBoxView.PlayingChanged += () => { /* 对话打开时暂停倒计时 */ };
```

`id` 必须等于文件 frontmatter 的 `id`，与文件名无关。目录：`Resources/Dialogue/`。

## 2. 怎么写一份对白

新建 `Assets/Resources/Dialogue/<随意>.txt`，格式对齐 DeadZone `config/narrative/dialogues`：

```markdown
---
id: dlg.tutorial.hero_select
title: 新手 · 选虫页开场
---

## 1. 旁白 · 开场
> mood: calm

旁白：把这只蛐蛐放进槽里。

## 2. 旁白 · 下一句
> mood: warm
> auto_skip: 0

旁白：点确定就能开打。
```

| 字段 | 位置 | 说明 |
| --- | --- | --- |
| `id` | frontmatter | 必填。`dlg.<phase>.<scope>.<slug>`，全小写+点+下划线 |
| `title` | frontmatter | 给人看，运行时不用 |
| `## N. 标题` | 一节一句 | 按出现顺序播 |
| `speaker：正文` 或 `speaker: 正文` | 节正文 | 全角/半角冒号都行 |
| `> mood:` | 节引用行 | `neutral/calm/warm/tense/urgent/angry/sad/fear/radio` |
| `> auto_skip:` | 节引用行 | 打完后自动进下一句的秒数；0 或不写 = 等点击 |
| `> cps:` | 节引用行 | 覆盖字/秒；不写则 32 × mood |
| `> avatar:` | 节引用行 | 立绘名，对应 `Resources/Dialogue/Portraits/<name>` |

也接受同结构 JSON（`id` + `lines[{speaker,text,mood,autoSkip,cps}]`）。markdown 优先给 AI 改。

加一句：复制一节 `##`，改说话人和正文。加一组：复制 `dlg.sample.box.txt`，换 `id`。

## 3. 点击与打字机

- 默认 32 字/秒，标点额外停顿（，。！？），短于 10 字整段秒出。
- 第一次点击：揭完全句。第二次：下一句。最后一句点完关闭。
- 时间用 `unscaledDeltaTime`，`timeScale=0` 也能点。
- 说话人空则隐藏名字。`avatar` 有图则显示立绘。
- sortingOrder 320，盖在结算（300）上面。

倒计时暂停不要改 `Time.timeScale`。监听 `DialogueBoxView.PlayingChanged` / `IsPlaying`，自己停 `BattleIntro` 或比赛时钟。

## 4. 不要搬什么

DeadZone 的 `phone_call` / `atlas_terminal` / `choice` / 立绘视频 / i18n 编译链 / StoryLineRunner 不进本项目。对白正文直接写中文，不写 `i18n_*`。
