<div align="center">

# All Seeing God

### R.E.P.O. 全知无敌 Mod

**无敌 · 强化生命与体力 · 常驻原生地图 · 怪物与宝物标记**

![Game](https://img.shields.io/badge/Game-R.E.P.O.-111111?style=for-the-badge)
![Loader](https://img.shields.io/badge/Loader-BepInEx_5-5B2C6F?style=for-the-badge)
![Platform](https://img.shields.io/badge/Platform-Windows_%7C_CrossOver-2471A3?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-2E8B57?style=for-the-badge)

</div>

---

All Seeing God 是一个面向 R.E.P.O. 的 BepInEx 客户端增强 Mod。它把生存强化和地图情报集中在一个轻量 DLL 中，并提供完整的配置文件。

> [!IMPORTANT]
> 仅建议在单人游戏或所有参与者知情同意的私人房间中使用。联机状态最终仍可能受到房主及游戏同步逻辑影响。

## 功能

| 模块 | 功能 | 默认值 |
| --- | --- | --- |
| 人物 | 阻止伤害并持续恢复生命 | 开启 |
| 人物 | 提高生命上限 | 500 |
| 人物 | 提高体力上限 | 100 |
| 地图 | 无需按 Tab，右上角常驻原生地图 | 开启 |
| 地图 | 红色标记怪物 | 开启 |
| 地图 | 显示宝物原生标记 | 开启 |
| 操作 | 临时隐藏或显示常驻地图 | `F8` |

## 环境要求

- Steam 版 R.E.P.O.
- BepInEx 5.4.23.5 或更高的 5.x 版本
- Windows，或通过 CrossOver/Wine 运行的 Windows 游戏环境

## 安装

1. 安装 BepInEx 5，并启动一次游戏以生成目录。
2. 从 `dist` 目录取得 `AllSeeingGod.dll`。
3. 创建并复制到：

   ```text
   REPO/BepInEx/plugins/AllSeeingGod/AllSeeingGod.dll
   ```

4. 重新启动游戏。日志中应出现：

   ```text
   Loading [All Seeing God / 全知无敌]
   ```

### CrossOver / Wine

在对应 Steam Bottle 的 Wine Configuration → Libraries 中加入：

```text
winhttp = Native, then Builtin
```

然后从同一个 Bottle 中的 Steam 启动游戏。请勿使用 macOS 原生 BepInEx；R.E.P.O. 需要 Windows Mono x64/BepInExPack。

## 使用与配置

进入可玩关卡后功能自动生效。Mac 键盘切换地图可能需要按 `fn + F8`。

首次运行会生成：

```text
REPO/BepInEx/config/cn.codex.REPO.AllSeeingGod.cfg
```

示例配置：

```ini
[01-人物]
无敌 = true
生命上限 = 500
体力上限 = 100

[02-地图]
常显地图 = true
显示怪物 = true
显示宝物 = true

[03-外观]
地图宽度 = 360
地图高度 = 300
地图缩放 = 2.25
地图透明度 = 0.9
地图开关键 = F8
```

## 联机说明

- 地图、标记与界面属于客户端显示功能。
- 无敌与本地体力由安装者客户端持续维护。
- 最大生命可能受到房主或原生升级数据同步影响。
- 本 Mod 不应被用于破坏公开房间或其他玩家的正常体验。

## 从源码构建

需要 Windows、游戏本体、BepInEx 5 和 .NET SDK 8：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build.ps1
```

非默认 Steam 路径：

```powershell
.\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\REPO"
```

构建脚本会生成 `AllSeeingGod.dll` 并复制到游戏插件目录。

## 故障排查

| 现象 | 检查项 |
| --- | --- |
| 日志中没有 Mod | DLL 路径、BepInEx 版本、`winhttp` 顶替 |
| 只有启动日志 | 确认安装的是最新 DLL，检查后续 Error 日志 |
| 联机生命回到 100 | 房主同步覆盖了客户端最大生命 |
| 地图没有出现 | 必须进入可玩关卡；检查是否与其他地图 Mod 冲突 |
| Mac 上 F8 无效 | 尝试 `fn + F8`，或修改配置键位 |

## 卸载

删除以下文件即可：

```text
REPO/BepInEx/plugins/AllSeeingGod/AllSeeingGod.dll
```

配置文件可以保留，也可以一并删除。

## License

[MIT](LICENSE)

<details>
<summary><strong>English summary</strong></summary>

All Seeing God is a configurable BepInEx client mod for R.E.P.O. It provides god mode, enhanced health and stamina, an always-on native minimap, and enemy/valuable markers. Install `AllSeeingGod.dll` into `BepInEx/plugins/AllSeeingGod/`. Use it only in single-player or consensual private lobbies.

</details>
