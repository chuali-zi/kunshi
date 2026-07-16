using System.IO;
using System.Reflection;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Persistence;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class CounterplayPerformanceRuntimeAdapterTests
    {
        private string _settingsPath;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _settingsPath = Path.Combine(Path.GetTempPath(), $"congame-settings-{System.Guid.NewGuid():N}.json");
            _root = new GameObject("CounterplayPerformanceRuntimeAdapterTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            DeleteIfExists(_settingsPath);
            DeleteIfExists(_settingsPath + ".bak");
            DeleteIfExists(_settingsPath + ".tmp");
        }

        [Test]
        public void ConfigureLoadsQteScaleAndD21SuccessMarksProgressWithoutChangingFlow()
        {
            GameSettingsStore store = new GameSettingsStore(_settingsPath);
            store.SaveAtomic(new GameSettingsV1 { qteThresholdScale = 0.6f });
            LoopDirector loop = _root.AddComponent<LoopDirector>();
            SetPrivateField(loop, "_progress", PersistentProgressV1.CreateNew());
            CounterplayPerformanceRuntimeAdapter adapter =
                _root.AddComponent<CounterplayPerformanceRuntimeAdapter>();
            adapter.Configure(null, loop, null, store);

            string requestedNext = null;
            adapter.NextOutcomeRequested += (_, _, next) => requestedNext = next;
            PerformanceBranchDefinition branch = new PerformanceBranchDefinition(
                "qte-success",
                true,
                "D22",
                true);
            adapter.OnBranchResolved(
                "D21",
                4,
                branch,
                CounterplayNarrativeOutcome.ScriptedFailure);

            Assert.That(adapter.AccessibilityThresholdScale, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(loop.Progress.d21Seen, Is.True);
            Assert.That(requestedNext, Is.EqualTo("D22"));
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
