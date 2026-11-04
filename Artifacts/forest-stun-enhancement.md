# 符箓镇晕眩增强

日期：2026-11-04
范围：迷踪林正式战斗；晕眩由符生的「符箓·镇」施加，持续 1 回合。

## 规则

- **完全沉默**：被晕眩的敌方无法进行任何操作——技能、移动、普攻、千面蓄力/释放、吞噬怪巡逻移动与自爆都不执行，只会原地待机，等于把行动整体延后 1 回合。正式配置的符箓镇有 **2 次**（工厂默认 1 次），一局最多可制造两个沉默窗口。
- **自爆延迟**：吞噬怪第 8 回合末仍处于晕眩时不自爆；解晕后的第一个回合末再自爆（`round >= 8` 且未晕眩才触发）。
- **自动效果暂停**：树精的自动成长、自动爆果与受击落地，以及吞噬怪的回合末吸雾，都在晕眩期间暂停。
- **亡语沉默**：被击杀时仍处于晕眩的吞噬怪不会触发死亡泄雾（亡语被沉默），携雾保留在死亡记录中，并记录「亡语被沉默」事件；非晕眩状态死亡照常泄雾。
- **不视为亡语的效果**：千面的阶段转换（掉元神/召唤腐卒）属于多阶段机制，不被晕眩沉默。
- 树精被晕眩时暂停自动成长、自动爆果与受击落地；沉默因此能提前预防这一轮瘴气生成，解晕后恢复。

## 实现

- [ForestRules.cs](../Assets/Scripts/Forest/ForestRules.cs)：`ValidateActor` 对晕眩单位只允许不带移动的 Wait；`RemoveDead` 对晕眩死亡的吞噬怪跳过 `SpillFog`，记录 `DeathrattleSilenced`。
- [ForestEnemies.cs](../Assets/Scripts/Forest/ForestEnemies.cs)：`ChooseEnemyAction` 对晕眩敌人直接返回无路径 Wait；`ResolveFruitBursts` 跳过晕眩树的成长/自动爆果；`ResolveDevourerSelfDestructs` 改为 `round >= 8` 且跳过晕眩，实现延迟自爆。
- [ForestRules.cs](../Assets/Scripts/Forest/ForestRules.cs)：`Attack` 在树精晕眩时也不执行 `ReleaseFruit`，受击落地同样被沉默。
- [ForestAudit.cs](../Assets/Scripts/Forest/ForestAudit.cs)：新增「亡语被沉默」事件文案。
- [ForestFeedback.cs](../Assets/Scripts/Forest/ForestFeedback.cs)、[ForestStatusDisplay.cs](../Assets/Scripts/Forest/ForestStatusDisplay.cs)：状态与摘要改为“无法行动/亡语沉默”。
- [LevelDesigner.cs](../Assets/Editor/LevelDesigner.cs)：符箓镇说明同步。

## 验证

- M4：113 条断言通过，新增覆盖：晕眩暂停树精成长、自动爆果与受击落地，解晕后恢复；晕眩延缓吞噬怪自爆并在解晕后恢复；晕眩死亡的吞噬怪不泄雾且携雾保留；晕眩敌人 AI 返回待机且无法执行普攻命令。
- M2 135、M3 43、技能 42、M5 35、协同 41、形态 30、增援 100、平衡 55、悔棋 43 条断言通过。
- 复现：`python Artifacts/verify_forest_m4.py`、`python Artifacts/verify_forest_m5.py`。

## 待确认

- 千面阶段转换目前不当作亡语，仍会在晕眩击杀时触发；若希望连阶段也沉默，需要额外处理，且会显著改变千面战。
- 被沉默的死亡吞噬怪保留携雾，不泄到地面；若希望清空携雾，需要额外定义并在守恒审计里处理。
- 树精在晕眩期间完全停止生成瘴气：自动成长、自动爆果、受击落地都不发生；解晕后按当前果级与剩余周期继续。
