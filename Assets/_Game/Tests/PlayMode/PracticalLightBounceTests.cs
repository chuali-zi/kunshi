using System.Collections;
using ConGame.Presentation.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace ConGame.Tests.PlayMode
{
    public sealed class PracticalLightBounceTests
    {
        [UnityTest]
        public IEnumerator TargetTracksSourceOffOnAndKeepsItsOwnRangeAndPosition()
        {
            GameObject sourceObject = new GameObject("SourceLight");
            GameObject targetObject = new GameObject("FillLight");
            GameObject bounceObject = new GameObject("PracticalLightBounce");

            Light source = sourceObject.AddComponent<Light>();
            source.color = new Color(1f, 0.2f, 0.1f);
            source.intensity = 2f;
            Light target = targetObject.AddComponent<Light>();
            target.range = 7f;
            target.transform.position = new Vector3(2f, 3f, 4f);
            PracticalLightBounce bounce = bounceObject.AddComponent<PracticalLightBounce>();
            bounce.Configure(source, target, 0.25f);

            yield return null;

            Assert.That(target.enabled, Is.True);
            Assert.That(target.intensity, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(target.color, Is.EqualTo(source.color).Using(ColorEqualityComparer.Instance));
            Assert.That(target.range, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(target.transform.position, Is.EqualTo(new Vector3(2f, 3f, 4f)).Using(Vector3ComparerWithEqualsOperator.Instance));

            source.enabled = false;
            yield return null;
            Assert.That(target.enabled, Is.False);

            source.enabled = true;
            yield return null;
            Assert.That(target.enabled, Is.True);

            Object.Destroy(sourceObject);
            Object.Destroy(targetObject);
            Object.Destroy(bounceObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InactiveSourceStopsFillAndDrapedColorAndIntensityRecover()
        {
            GameObject sourceObject = new GameObject("SourceLight");
            GameObject targetObject = new GameObject("FillLight");
            GameObject bounceObject = new GameObject("PracticalLightBounce");

            Light source = sourceObject.AddComponent<Light>();
            source.color = Color.white;
            source.intensity = 2f;
            Light target = targetObject.AddComponent<Light>();
            PracticalLightBounce bounce = bounceObject.AddComponent<PracticalLightBounce>();
            bounce.Configure(source, target, 0.5f);

            sourceObject.SetActive(false);
            yield return null;
            Assert.That(target.enabled, Is.False);

            sourceObject.SetActive(true);
            source.color = new Color(0.42f, 0.055f, 0.035f);
            source.intensity = 0.7f;
            yield return null;

            Assert.That(target.enabled, Is.True);
            Assert.That(target.color, Is.EqualTo(source.color).Using(ColorEqualityComparer.Instance));
            Assert.That(target.intensity, Is.EqualTo(0.35f).Within(0.0001f));

            Object.Destroy(sourceObject);
            Object.Destroy(targetObject);
            Object.Destroy(bounceObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelfReferenceNeverDisablesOrRewritesSourceLight()
        {
            GameObject sourceObject = new GameObject("SourceLight");
            Light source = sourceObject.AddComponent<Light>();
            source.color = new Color(0.3f, 0.4f, 0.5f);
            source.intensity = 1.7f;
            PracticalLightBounce bounce = sourceObject.AddComponent<PracticalLightBounce>();
            bounce.Configure(source, source, 0.01f);

            yield return null;

            Assert.That(source.enabled, Is.True);
            Assert.That(source.color, Is.EqualTo(new Color(0.3f, 0.4f, 0.5f)).Using(ColorEqualityComparer.Instance));
            Assert.That(source.intensity, Is.EqualTo(1.7f).Within(0.0001f));

            Object.Destroy(sourceObject);
            yield return null;
        }
    }
}
