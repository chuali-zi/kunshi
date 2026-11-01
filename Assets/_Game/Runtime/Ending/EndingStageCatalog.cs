using System;

namespace ConGame.Runtime.Ending
{

    /// <summary>由设置构建 E2/E3 阶段列表。纯 C#,供编排器与 EditMode 测试共用。</summary>
    public static class EndingStageCatalog
    {
        public static EndingStageDefinition[] BuildE2Stages(EndingSequenceSettings settings)
        {
            EndingSequenceSettings s = Normalize(settings);
            return new[]
            {
                new EndingStageDefinition(EndingStageId.E2FirstPersonTakeover, s.E2TakeoverSeconds),
                new EndingStageDefinition(EndingStageId.E2FreeControl, s.E2FreeControlSeconds),
                new EndingStageDefinition(
                    EndingStageId.E2HuntToDeath,
                    0f,
                    waitForExternalCompletion: true,
                    timeoutSeconds: s.E2FightTimeoutSeconds),
                new EndingStageDefinition(EndingStageId.E2Fall, s.E2FallSeconds),
                new EndingStageDefinition(EndingStageId.E2Blackout, s.E2BlackoutSeconds)
            };
        }

        public static EndingStageDefinition[] BuildE3Stages(EndingSequenceSettings settings)
        {
            EndingSequenceSettings s = Normalize(settings);
            return new[]
            {
                new EndingStageDefinition(EndingStageId.E3SitChair, s.E3SitChairSeconds),
                new EndingStageDefinition(EndingStageId.E3KillerApproach, s.E3KillerApproachSeconds),
                new EndingStageDefinition(EndingStageId.E3ClockTick, s.E3ClockTickSeconds),
                new EndingStageDefinition(EndingStageId.E3BackupShot, s.E3ShotSilenceSeconds),
                new EndingStageDefinition(EndingStageId.E3Title, s.E3TitleHoldSeconds)
            };
        }

        private static EndingSequenceSettings Normalize(EndingSequenceSettings settings)
        {
            if (settings == null)
            {
                return EndingSequenceSettings.Default;
            }

            return settings.CloneNormalized();
        }
    }
}
