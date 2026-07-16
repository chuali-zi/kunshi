using System.Collections;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace ConGame.Tests.PlayMode
{

    public sealed class LoopResetPlayModeTests
    {
        [UnityTest]
        public IEnumerator RigidbodyStateReturnsToCapturedPose()
        {
            GameObject root = new GameObject("ResetRoot");
            LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
            registry.Configure(root.transform);

            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.transform.SetParent(root.transform);
            item.transform.localPosition = new Vector3(1f, 2f, 3f);
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.isKinematic = true;
            item.AddComponent<RigidbodyResettable>();

            yield return null;
            registry.CaptureInitialState();
            body.isKinematic = false;
            item.transform.SetParent(null);
            item.transform.position = new Vector3(8f, 9f, 10f);
            body.linearVelocity = Vector3.one * 4f;

            registry.ResetAll(1, 2);

            Assert.That(item.transform.parent, Is.EqualTo(root.transform));
            Assert.That(item.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero).Using(Vector3ComparerWithEqualsOperator.Instance));
            Object.Destroy(root);
            yield return null;
        }
    }
}
