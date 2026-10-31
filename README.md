# CalmRopeDown

[English](#english) · [中文](#中文)

![version](https://img.shields.io/badge/version-1.3.0-informational)

![game](https://img.shields.io/badge/The%20Long%20Dark-2.55-success)

![loader](https://img.shields.io/badge/MelonLoader-0.7.3-blueviolet)

![license](https://img.shields.io/badge/license-MIT-blue)

---

## English

A The Long Dark mod that turns climbing *down* a climbing rope into a constant-speed slide and can cut the stamina/fatigue drain of descending to any fraction — while climbing *up* stays exactly vanilla.

### Features

**Constant-speed descent**

- Slide down a climbing rope at one steady speed instead of the vanilla burst-by-burst crawl; the default speed is the vanilla "fully rested" descent speed.
- Switch to Fixed mode and pick any speed from 0.5 to 12 m/s.
- Turning the mod off — even in the middle of a climb — restores vanilla descent behaviour immediately, no restart.

**Lighter drain**

- Stamina and fatigue drain while descending = vanilla × a multiplier you set; at the default 0%, descending costs nothing at all.
- Climbing up, holding still and falling keep vanilla speed and vanilla drain — bit for bit.

**What it does not do**

- Nothing pops up in the middle of the screen, and it never steals a vanilla key.
- Ladders, rock climbs and every other vertical movement are untouched.
- While sliding, your character holds one fixed glide posture and footsteps stop firing — that is what constant speed looks like. The climb-up animation is unaffected.

### Requirements

- The Long Dark **2.55** (built against 2.55)
- [MelonLoader](https://melonwiki.xyz/) **0.7.3** or newer (tested on 0.7.3)
- **ModSettings** (required; the settings page depends on it)

### Installation

1. Make sure `MelonLoader/` exists in the game folder.
2. Put `CalmRopeDown.dll` into `TheLongDark/Mods`.
3. Start the game. The MelonLoader log should list CalmRopeDown and report three applied patches.

### Usage

1. Open **Options → Mod Settings → CalmRopeDown**, pick a mode, press CONFIRM — settings apply immediately.
2. Walk to a climbing rope and climb down: you slide at a constant speed.
3. Toggle the mod off at any time (even mid-climb) and vanilla behaviour returns on the spot.

A copy of the settings is kept in `Mods/CalmRopeDown.json`, written when you press CONFIRM.

### Settings

| Setting | Effect | Default |
|---|---|---|
| 启用登山绳下滑修改 / Enable Rope Descent Mod | Master toggle. **Off** restores vanilla behaviour immediately, no restart | On |
| 速度模式 / Speed Mode | Where the constant speed comes from: "Vanilla Rested" = the vanilla fully-rested descent speed; "Fixed" = the next row | Vanilla Rested |
| 固定下滑速度 (米/秒) / Fixed Descent Speed (m/s) | Line speed while descending, only in Fixed mode. **Higher is faster**, range 0.5–12 | 2.00 |
| 耐力/精力消耗倍率 / Stamina & Fatigue Drain Multiplier | Descent drain = vanilla × this. **0 = no drain**, 1.0 = vanilla | 0% |
| 调试日志 / Debug Logging | Logs climbing-state changes and live speed to the MelonLoader log | Off |

### Compatibility

- Affects climbing-rope climbing only; ladders, rock climbs and other vertical movement are not touched.
- It hooks three vanilla climbing methods with plain before/after callbacks and overwrites the inputs the game reads — no method-body rewriting, so it does not structurally conflict with other climb mods even when they patch the same methods.
- If another mod also changes the descent speed or drain, the last writer wins; this mod rewrites its values every frame while enabled, so it usually wins.

### FAQ

- **Why does my character glide in one pose with no footsteps while descending?** That is the constant-speed slide at work — see "What it does not do" above. Climbing up still plays the normal animation.
- **I moved a slider but nothing changed.** Settings apply when you press CONFIRM, not while you drag the slider.
- **The descent speed does not match what I set.** In "Vanilla Rested" mode the speed is your game's own vanilla fully-rested descent value; switch to "Fixed" to control it exactly.

### Uninstall

Delete `TheLongDark/Mods/CalmRopeDown.dll` (and optionally `Mods/CalmRopeDown.json`).

No fields are written to the save. The overwritten values live only in memory and snap back to vanilla the moment you leave the rope, change scene or quit.

See [ChangeLog.md](ChangeLog.md) for version history.

### License

MIT — see [LICENSE](LICENSE).

---

## 中文

让沿登山绳**向下攀爬**变成匀速滑降，下行时的耐力与精力消耗可以任意缩放——而**向上攀爬**与原版逐位一致。

### 功能

**匀速下滑**

- 沿登山绳向下时以恒定速度滑降，不再是原版一段一顿的爬行；默认速度取原版「体力充沛」状态的下行速度。
- 切到固定速度模式后，可在 0.5–12 米/秒之间任选。
- 关掉开关（哪怕正在绳上）会立即恢复原版下行行为，无需重启。

**减轻下行消耗**

- 下行时的耐力与精力消耗 = 原版 × 你设的倍率；默认 0% 时下行完全不消耗。
- 上行、抓握、坠落的速度与消耗与原版逐位一致。

**不会发生什么**

- 屏幕中间不弹任何提示，也不占用任何原版按键。
- 梯子、岩石攀爬等其他垂直移动一概不碰。
- 滑降时角色保持单一固定姿势、脚步声不再触发——这就是匀速滑降的表现；上攀动画不受影响。

### 前置条件

- 《The Long Dark》**2.55**（本 mod 针对 2.55 编译）
- [MelonLoader](https://melonwiki.xyz/) **0.7.3** 或更高（实测环境 0.7.3）
- **ModSettings**（必需，设置页依赖它）

### 安装

1. 确认游戏目录下已有 `MelonLoader/` 文件夹。
2. 把 `CalmRopeDown.dll` 放进 `TheLongDark/Mods`。
3. 启动游戏。MelonLoader 日志里应出现 CalmRopeDown，并报告已挂载 3 个补丁。

### 使用

1. 打开**选项 → Mod Settings → CalmRopeDown**，选好模式后按 CONFIRM——设置即时生效。
2. 走到登山绳处向下爬：角色匀速滑降。
3. 随时把开关关掉（哪怕正在绳上），原版行为当场恢复。

设置副本保存在 `Mods/CalmRopeDown.json`，按 CONFIRM 时写入。

### 设置

| 项 | 说明 | 默认 |
|---|---|---|
| 启用登山绳下滑修改 / Enable Rope Descent Mod | 总开关。**关**即立即恢复原版行为，无需重启 | 开 |
| 速度模式 / Speed Mode | 匀速的来源：「原版充沛速度」= 原版体力充沛状态的下行速度；「固定速度」= 下一项 | 原版充沛速度 |
| 固定下滑速度 (米/秒) / Fixed Descent Speed (m/s) | 固定速度模式下的实际线速度。**越大越快**，范围 0.5–12 | 2.00 |
| 耐力/精力消耗倍率 / Stamina & Fatigue Drain Multiplier | 下行消耗 = 原版 × 此倍率。**0 = 不消耗**，1.0 = 原版 | 0% |
| 调试日志 / Debug Logging | 把攀爬状态切换与实时速度写进 MelonLoader 日志 | 关 |

### 兼容性

- 只作用于登山绳攀爬；梯子、岩石攀爬等其他垂直移动不受影响。
- 对三个原版攀爬方法只挂普通前后回调并覆写游戏读取的输入值——不改方法体，即使其他攀爬 mod 补丁同样的方法也不产生结构冲突。
- 若别的 mod 也改下行速度或消耗，后写入者生效；本 mod 启用时每帧重写，通常为本 mod 胜出。

### 常见问题

- **为什么下滑时角色保持一个姿势、没有脚步声？** 这正是匀速滑降的表现——见上文「不会发生什么」。上攀仍播放正常动画。
- **拖了滑条但没变化。** 设置在按下 CONFIRM 时才生效，拖动滑条的过程中不生效。
- **下行速度和设置值对不上。** 「原版充沛速度」模式用的是你游戏自己的原版充沛下行值；想精确控制请切到「固定速度」。

### 卸载

删掉 `TheLongDark/Mods/CalmRopeDown.dll` 即可（`Mods/CalmRopeDown.json` 可一并删除）。

存档里不会留下本 mod 的任何字段。被覆写的值只存在于内存中，离绳、切场景或退出游戏的瞬间就恢复原版。

更新记录见 [ChangeLog.md](ChangeLog.md)。

### 许可证

MIT —— 见 [LICENSE](LICENSE)。
