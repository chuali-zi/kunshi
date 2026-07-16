using System.IO;
using ConGame.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class GameSettingsTests
    {
        [Test]
        public void NormalizeClampsEveryNumericSetting()
        {
            GameSettingsV1 settings = new GameSettingsV1
            {
                schemaVersion = -3,
                masterVolume = 8f,
                lookSensitivity = 0f,
                qteThresholdScale = 0.1f,
                crtIntensity = -2f
            };

            settings.Normalize();

            Assert.That(settings.schemaVersion, Is.EqualTo(GameSettingsV1.CurrentSchemaVersion));
            Assert.That(settings.masterVolume, Is.EqualTo(1f));
            Assert.That(settings.lookSensitivity, Is.EqualTo(0.25f));
            Assert.That(settings.qteThresholdScale, Is.EqualTo(0.5f));
            Assert.That(settings.crtIntensity, Is.EqualTo(0f));
        }

        [Test]
        public void AccessibilityOnlyLowersConfiguredQteThreshold()
        {
            GameSettingsV1 settings = new GameSettingsV1 { qteThresholdScale = 0.5f };

            Assert.That(settings.AdjustQteThreshold(7), Is.EqualTo(4));
            Assert.That(settings.AdjustQteThreshold(1), Is.EqualTo(1));
        }

        [Test]
        public void LookSettingsApplySensitivityAndOptionalYInversion()
        {
            GameSettingsV1 settings = new GameSettingsV1
            {
                lookSensitivity = 2f,
                invertLookY = true
            };

            Assert.That(settings.ApplyLookSettings(new Vector2(1f, 3f)), Is.EqualTo(new Vector2(2f, -6f)));
        }

        [Test]
        public void SettingsMenuWrapsAndAdjustsSelectedRow()
        {
            GameSettingsV1 settings = new GameSettingsV1();
            SettingsMenuModel model = new SettingsMenuModel(settings);

            model.MoveSelection(-1);
            Assert.That(model.SelectedRow, Is.EqualTo(SettingsRow.Back));
            model.MoveSelection(1);
            Assert.That(model.SelectedRow, Is.EqualTo(SettingsRow.MasterVolume));

            Assert.That(model.AdjustSelected(-1), Is.True);
            Assert.That(settings.masterVolume, Is.EqualTo(0.9f).Within(0.001f));
        }
    

[Test]
        public void SettingsStorePersistsNormalizedValues()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ConGameSettingsTests");
            string path = Path.Combine(directory, "settings-" + System.Guid.NewGuid().ToString("N") + ".json");
            GameSettingsStore store = new GameSettingsStore(path);
            GameSettingsV1 saved = new GameSettingsV1
            {
                masterVolume = 0.35f,
                lookSensitivity = 9f,
                qteThresholdScale = 0.75f,
                reduceFlashing = true,
                crtIntensity = 0.4f
            };

            try
            {
                Assert.That(store.SaveAtomic(saved), Is.True);
                GameSettingsV1 loaded = store.Load();

                Assert.That(loaded.masterVolume, Is.EqualTo(0.35f).Within(0.001f));
                Assert.That(loaded.lookSensitivity, Is.EqualTo(3f));
                Assert.That(loaded.qteThresholdScale, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(loaded.reduceFlashing, Is.True);
                Assert.That(loaded.crtIntensity, Is.EqualTo(0.4f).Within(0.001f));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
}
}
