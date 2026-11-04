namespace Mizonglin.Demo
{
    // 表现层节奏常量集中地。
    // 所有会阻塞玩家输入、或直接决定一次表现占用多长的时长都在这里定义；
    // 调用点只允许引用本类成员，不再保留裸字面量，便于统一按比例调参。
    internal static class BattlePacing
    {
        // 开场"胜利条件"横幅（Announcements.ShowVictoryConditions）
        public const float VictoryIn = .24f;
        public const float VictoryHold = 2.2f;
        public const float VictoryOut = .24f;

        // 回合/敌方回合切换徽章（Announcements.ShowTurnTransition）
        public const float TurnSealIn = .16f;
        public const float TurnSealHold = .34f;
        public const float TurnSealOut = .16f;

        // 我方技能横框（Announcements.ShowSkillCutIn）
        // 分级：同一次战斗内同一 (角色, 技能名) 首次出现用完整版，之后用精简版。
        public const float SkillInFirst = .30f;
        public const float SkillHoldFirst = 1f;
        public const float SkillOutFirst = .20f;
        public const float SkillInRepeat = .20f;
        public const float SkillHoldRepeat = .35f;
        public const float SkillOutRepeat = .14f;

        // 敌方技能提示（Announcements.ShowEnemySkill / FocusEnemyForSkill）
        public const float EnemySkillFocus = .22f;
        public const float EnemySkillIn = .16f;
        public const float EnemySkillHold = .45f;
        public const float EnemySkillOut = .14f;

        // 打击结算停顿（CombatVisuals.PlayForestImpacts）
        public const float ImpactDamageHold = .20f;
        // 唤灵/咒言分支的等待。注意 BuffPulses.BuffPulseDuration(.46) 仍驱动光效本身，不要改成这里。
        public const float BuffPulseWait = .28f;

        // 行动前摇（CombatVisuals.AnimateActionLeadIn）
        public const float ActionLeadIn = .12f;

        // 敌方移动速度：世界单位/秒，格子间距为 1（CombatVisuals.AnimateEnemyTravel）
        public const float EnemyTravelSpeed = 11f;

        // 奖励弹窗总时长（Rewards.ShowCollectedReward）
        public const float RewardPopup = 1.2f;
    }
}
