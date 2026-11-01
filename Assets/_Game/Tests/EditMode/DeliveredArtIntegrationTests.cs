using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class DeliveredArtIntegrationTests
    {
        [Test]
        public void EveryM1PerformanceReferencesBothCharacterAnimations()
        {
            string[] performanceGuids = AssetDatabase.FindAssets(
                "t:M1GreyboxPlayableAsset",
                new[] { "Assets/_Game/Content/Performances" });

            Assert.That(performanceGuids, Has.Length.EqualTo(23));
            for (int i = 0; i < performanceGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(performanceGuids[i]);
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                SerializedObject serialized = new SerializedObject(asset);
                Assert.That(serialized.FindProperty("playerAnimation").objectReferenceValue, Is.Not.Null, path);
                Assert.That(serialized.FindProperty("killerAnimation").objectReferenceValue, Is.Not.Null, path);
            }
        }

        [Test]
        public void DeliveredAnimationsImportAsHumanoidClips()
        {
            string[] animationGuids = AssetDatabase.FindAssets(
                "t:Model",
                new[] { "Assets/_Game/Art/Animations" });
            int deliveredCount = 0;

            for (int i = 0; i < animationGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(animationGuids[i]);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("POSE_") && !name.StartsWith("PAIR_") &&
                    name != "AN_Player_Pickup" && name != "AN_Player_Throw" &&
                    !name.StartsWith("AN_Killer_"))
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human), path);
                Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel), path);
                Assert.That(importer.clipAnimations, Is.Not.Empty, path);
                if (name.StartsWith("POSE_"))
                {
                    Assert.That(importer.clipAnimations[0].loopTime, Is.True, path);
                }

                AnimationClip clip = null;
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (assets[assetIndex] is AnimationClip candidate &&
                        !candidate.name.StartsWith("__preview__"))
                    {
                        clip = candidate;
                        break;
                    }
                }

                Assert.That(clip, Is.Not.Null, path);
                deliveredCount++;
            }

            Assert.That(deliveredCount, Is.GreaterThanOrEqualTo(24));
        }
    }
}
