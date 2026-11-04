# 悔棋功能记录

日期：2026-11-04
实现范围：迷踪林正式战斗（`ForestEnabled`）；旧原型不接入。

## 口径

- **一步 = 一名角色同一回合内全部已提交的操作**：移动+主要行动、待机、免费技能（展翼/疾行）、元神操作（携带/投掷/吞下）。选择角色、移动预览、取消与镜头拖动不算。
- **最多回退 3 步**：按角色本回合首次行动的先后顺序回退；同一角色同一回合只记一个快照，回头追加的操作仍归入原快照。重开清空。
- **允许跨回合**：回退会连同该步之后的敌方阶段与回合末结算一起撤销。随机状态同步还原，所以重复同一操作时敌方按原样重演，不会重掷。
- **计时不回退**：本局用时保留实际累计值；战报记录回退后的时间线与总用时。
- **教程已展示标记不回退**：同一局不会因悔棋重复弹出教程。
- **结束回合不单独记一步**：误按结束回合时，悔棋回到最后一名行动角色操作前，该角色可立即重做，敌方阶段随之撤销。
- **不做重做、不做二次确认**；回退即丢弃后续时间线。
- **可用条件**：战斗进行中（`InProgress`）、不在动画/结算忙碌、我方阶段、历史非空。教程弹窗、敌方阶段、胜负结算后不可用。

## 实现

- [ForestSession](../Assets/Scripts/Forest/ForestBattleState.cs)（同文件）：`UndoPoint { state, actorId, round }` 栈（上限 3），`RecordUndoPoint` 按 `(round, actorId)` 去重后压入快照，`TryUndo` 以 `CommitFrom` 原位恢复并保留计时，`Restart` 清空。
- [ForestBattleState.CommitFrom](../Assets/Scripts/Forest/ForestBattleState.cs)：恢复快照时删除快照中不存在的单位，避免千面阶段/回合末召唤的腐卒在回退后残留。
- [BattleDemo.Forest.cs](../Assets/Scripts/Presentation/BattleDemo.Forest.cs)：两处玩家提交（主要行动、免费技能）在 `TryExecute` 前记录，失败时丢弃；`UndoForestStep` 清理预览/选中/悬停/特效，随后 `SyncForest → ResetFigures → SyncMapVisuals → Refresh`，重选被回退的角色并强制刷新战斗记录。
- [BattleDemo.Feedback.cs](../Assets/Scripts/Presentation/BattleDemo.Feedback.cs)：右上「悔棋」按钮（130×48、ink 底、SharpText，与「战斗记录」同款），位于战斗记录下方，「本局用时」从 -150 下移到 -202；`TickForestFeedback` 维护可点状态。
- 检查入口：[ForestUndoChecks.cs](../Assets/Editor/ForestUndoChecks.cs)（43 条断言）、菜单「紫龙 Demo → 迷踪林 → 执行悔棋逻辑验收」、[verify_forest_undo.py](../Artifacts/verify_forest_undo.py)、[UndoIntegration.cs](../Artifacts/UndoIntegration.cs)、[verify_undo_ui.py](../Artifacts/verify_undo_ui.py)。

## 验证

- 逻辑检查：`python Artifacts/verify_forest_undo.py`，43 条断言通过，覆盖角色整回合合并、三步上限、跨回合恢复、召唤单位清理、被拒指令不占步、日志截断、计时与随机状态规则。结果：`Artifacts/forest-undo-verification.txt`。
- Unity Play 检查：`python Artifacts/verify_undo_ui.py`，在实际 Play 中验证按钮位置/尺寸/样式、忙碌与空历史禁用、真实「待机」行动后可点、点击一次撤回整回合并重选角色、按钮再次禁用。结果：`Artifacts/undo-ui-verification.txt`；截图：`Artifacts/undo-ui.png`。
- 回归：M1–M4、技能改版、M5 全部通过。
- M5 检查原先使用随机会话种子，千面阶段召唤的腐卒可能随机堵住检查里硬编码的携带路径，导致偶发失败；已在该检查的 `Fresh` 中固定 `randomState = 42`，使回归可重复。此问题与悔棋改动无关。
- 未执行：人工整局试玩与 3–8 分钟时长验收。

## 已知边界

- 无重做；一次回退会丢弃后续时间线。
- 同一角色在其他人行动后回头追加操作时，仍归入其本回合原有步骤；回退顺序按各角色本回合首次行动的先后，而非最近点击顺序。
- 快照是 3 份全量深拷贝（含事件日志），本 demo 规模可接受。
- 回退会改写战报记录的行动历史；计时保持真实累计。
