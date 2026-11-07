# Contributing

Thank you for helping improve **Train Train / 混乱火车**.

## 开发约定

1. 使用 Unity 2022.3 LTS。
2. 新增关卡时保持 `Train Train / 混乱火车` 的统一命名，并保留 `© 2026 X4CE` 版权文本。
3. 优先使用运行时代码与内置资源，避免引入来源或授权不清的素材。
4. 提交前打开 `Assets/Scenes/Main.unity` 完整游玩一次，并确认 24 个已有关卡仍可进入。
5. 所有贡献默认以本项目的 MIT License 发布。

## 新增地图

地图与关卡位于 `Assets/Scripts/Data/LevelCatalog.cs`。每张地图需要：

- 4 个同色发车站与 4 个同色终点站
- 连通且能形成多种路线的铁路图
- 至少两个会产生真实路线选择的道岔
- 与现有区域不同的空间轮廓

