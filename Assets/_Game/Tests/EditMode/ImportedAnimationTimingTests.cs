using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class ImportedAnimationTimingTests
    {
        [TestCase("PAIR_D21_PlayerSide")]
        [TestCase("PAIR_D21_KillerSide")]
        [TestCase("AN_Killer_Fall")]
        [TestCase("AN_Killer_DrawBackupFire")]
        [TestCase("AN_Player_Swing")]
        public void ImportedEventsDoNotExtendTheSourceTake(string name)
        {
            string path = "Assets/_Game/Art/Animations/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var take = importer.clipAnimations.Single(c => c.name == name);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Single(c => c.name == name);
            Assert.That(take.events.All(e => e.time >= 0f && e.time <= 1f), Is.True,
                "ModelImporter events use normalized time, not seconds.");
            float sourceSeconds = (take.lastFrame - take.firstFrame) / clip.frameRate;
            Assert.That(clip.length, Is.EqualTo(sourceSeconds).Within(1f / clip.frameRate),
                "Out-of-range imported markers must not stretch the playable into a frozen tail.");
        }
    }
}
