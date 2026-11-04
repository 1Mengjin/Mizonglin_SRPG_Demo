# 状态栏仅显示局内效果

移除状态栏中的预览、行动次数、技能剩余次数、技能冷却、普通落地状态、护卫/起咒等常驻被动说明，以及千面阶段介绍。技能次数和冷却继续由原有技能按钮显示。

保留实际生效的瘴毒、蚀痕、晕眩、唤灵、咒言、反伤、净行、携带元神、飞行、攻守形态、待切换标记、濒死与蓄力等。树精仅在果级高于基础1级时显示瘴果，吞噬怪仅在实际携雾大于0时显示携雾。

状态栏从正式战斗状态读取，移动预览副本中尚未提交的咒言、蚀痕等不作为已经施加的效果展示。没有效果时同时隐藏标题、查看提示、分隔线与图标区，缩短角色卡。

文案编辑窗口隐藏已停用的状态条目；旧资源中的文案保留以避免丢失用户编辑，运行时不会据此重新生成已删除的状态。

验证：完整运行时/编辑器编译与M1回归通过；`python Artifacts/verify_status_bar.py` 的22项针对性检查通过。`python Artifacts/verify_battle_ui.py --descriptions` 实际播放确认无效果角色在预览时不显示状态栏、未提交预览Buff不显示、真实效果仍显示且文案可修改和保存。

截图：`Artifacts/ui-15-clean-status-card.png`（无效果的角色移动预览）及 `Artifacts/ui-16-active-effects-card.png`（隔离测试中施加效果的角色）。完整检查结果为 `Artifacts/status-bar-verification.txt` 和 `Artifacts/description-ui-verification.txt`。
