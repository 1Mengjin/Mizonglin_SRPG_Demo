# 迷踪林里程碑 1 验收记录

日期：2026-11-01。范围依据修改后的三份参考文档及《迷踪林_规则与实现方案草案》，仅实现 A01—A04。

## 交付内容

- `Assets/Data/ForestScenario.asset`：正式迷踪林配置，8 个模板、11 个实例及完整参数。
- `Assets/Scripts/Forest/ForestDefinition.cs`：配置结构、独立规则版本、参数校验及显式深拷贝。
- `Assets/Scripts/Forest/ForestDefaults.cs`：按台账创建默认值，只复用旧地图几何，不复用旧单位或战斗规则。
- `Assets/Scripts/Forest/ForestScenario.cs`：Unity 配置资源和独立战局创建入口。
- `Assets/Scripts/Forest/ForestBattleState.cs`：局内状态容器、独立初始快照与重开；不推进回合。
- `Assets/Editor/ForestDataWindow.cs`：菜单「紫龙 Demo → 迷踪林 → 里程碑1数据与状态」。
- `Assets/Editor/ForestDataChecks.cs`、`ForestMilestoneOneChecks.cs`：数据验收与 Unity 原生序列化验收入口。

## A01：模板与实例

通过：C01—C04 与 M01—M04 共 8 模板、11 实例；角色名称、职业、阵营、属相、基础生命/攻防/移动/射程与台账逐项核对。符箓、治疗/涤尘、飞行权限、攻守形态、爆果、吸雾/增长关闭、千面阶段和公共规则参数通过。树精与吞噬怪 `hasBasicAttack=false`，攻击/射程为 0。

## A02：地图与布置

通过：18×14、149 格四邻接连通可走格、15 格初始雾；地形/区域/初始雾与实际 C# 地图工厂逐格一致。四人出生格、两树、千面、三腐卒及吞噬怪 `(15,6)` 与台账一致。根须来源为 M01A；巡逻闭环为 `(15,6) → (16,6) → (16,7) → (15,7)`，下一索引为 1，即 `(16,6)`。

## A03：状态隔离与重开

通过：分别创建两个战局，修改其中一个的生命、位置、行动权、普通瘴毒、蚀痕、元神携带、净行额度、技能次数/CD、姿态/形态、果级、阶段/掉落标记、污染、地图图层、根须/巡逻、规则、结果及历史，另一个战局与配置不受影响。

同模板两树的状态互不影响；返回的模板参数是独立副本；死亡吞噬怪失去占位但携雾记录仍保留。修改原始配置后，连续三次重开仍从本局创建时的快照恢复。重新初始化才载入配置后续编辑。

初始化位于我方阶段开始前，尚未执行保护选择、技能、移动或任何战斗结算。青羽初始落地未锁定，阿双初始守形态无保护关系，树精果级 1/待爆回合 6，千面阶段 1，资源与历史为空。

## A04：拒绝非法配置与兼容

通过：非法格式/规则版本、缺失主体、重复模板/实例、未知引用、非法属性/属相/阵营、攻击能力不一致、缺失能力参数、非法飞行权限、启用未定义增长、千面阶段生命不一致、错误地图图层、非法地形/区域、障碍雾、越界/重叠出生点、重复根须/错误来源、非法巡逻、错误元神容量等均拒绝初始化。

Unity 实际加载正式 `.asset` 后执行同一套数据检查；原生 JSON 序列化→反序列化→再验证→序列化一致性通过。实际旧 `BattleScenario.asset`、其模板与地图 JSON 可读取并初始化；旧默认地图与旧迷踪林几何地图的初始化通过。

## 验证证据

- `python Artifacts/verify_forest_m1.py`：主项目全部运行时与编辑器源文件编译通过；实际 C# 数据检查通过。
- `Artifacts/forest-m1-standalone-verification.txt`：A01—A04 独立数据检查报告。
- `python Artifacts/run_forest_m1_unity.py`：用相同源码、元文件和配置资源创建隔离验证工程，运行 Unity 2022.3.62f3c1 原生检查通过。原始编辑器和场景无需关闭。
- `Artifacts/forest-m1-unity-batch-verification.txt`：正式/旧资源加载、原生 JSON 往返、实际旧地图初始化报告。
- `Artifacts/forest-m1-unity-batch.log`：Unity 执行日志，包含 `FOREST_M1_NATIVE_PASS`，进程退出码 0。
- `Artifacts/forest-m1-original-files.json`：实施前旧代码、旧配置及场景的哈希基线；最终核对见 `forest-m1-scope-verification.txt`。

## 验证边界

没有实现或验收 A05—A20。没有切换主场景的默认配置，没有修改原有战斗、镜头、轮盘或地图编辑器行为，没有生成新可执行构建。窗口操作授权等待超时，状态查看窗口尚未进行实际画面和按钮点击验证；编辑器代码编译通过。

验证脚本使用本机已安装 Unity 路径。独立验证工程位于 `Artifacts/ForestM1UnityProject`，已单独忽略生成目录。配置资源一次性生成工具 `create_forest_m1_asset.py` 使用独立检查导出的真实 C# 默认数据，拒绝覆盖已有配置；后续正常编辑使用 Unity Inspector。
