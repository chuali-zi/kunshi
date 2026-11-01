using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;

namespace ConGame.Presentation.Vfx
{
    /// <summary>
    /// 语义 VFX 标识。绑定表把玩法事件(ImpactResolved、ShotFired、破坏状态、
    /// Performance 语义)翻译成这些稳定 ID,运行时不引用具体美术资产。
    /// </summary>
    public enum VfxCueId
    {
        MuzzleFlash = 0,
        ImpactSpark = 1,
        BloodImpact = 2,
        WoodDebris = 3,
        CeramicShards = 4,
        MirrorCrack = 5,
        WallDust = 6,
        LampGlassBurst = 7,
        TelevisionBurst = 8
    }

    /// <summary>
    /// 无绑定时的默认命中语义映射,纯 C# 可单测。
    /// </summary>
    public static class VfxImpactSemantics
    {
        public static bool TryGetDefaultCue(ImpactKind kind, HitResult result, out VfxCueId cue)
        {
            cue = VfxCueId.ImpactSpark;
            switch (kind)
            {
                case ImpactKind.Bullet:
                    cue = VfxCueId.ImpactSpark;
                    return true;
                case ImpactKind.Heavy:
                case ImpactKind.Projectile:
                    cue = VfxCueId.WallDust;
                    return true;
                default:
                    return result == HitResult.Broke;
            }
        }
    }
}
