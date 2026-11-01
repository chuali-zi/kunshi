using System.Collections;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Lighting;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace ConGame.Tests.PlayMode
{
    public sealed class BreakableLightEmissionPlayModeTests
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [UnityTest]
        public IEnumerator LampEmissionGoesBlackWhenLightIsOffAndRestoresWhenOn()
        {
            GameObject root = new GameObject("Lamp");
            GameObject intact = CreateEmissionVisual(root.transform, "Lamp_Intact");
            Light emitter = CreateEmitter(root.transform);
            BreakableLight state = root.AddComponent<BreakableLight>();
            BreakableLightPresenter presenter = root.AddComponent<BreakableLightPresenter>();
            state.Configure(emitter);
            presenter.Configure(state, intact, null, emitter);

            yield return null;

            Color original = ReadEmission(intact);
            Assert.That(original.r, Is.GreaterThan(0.1f));
            emitter.enabled = false;
            yield return null;
            Assert.That(ReadEmission(intact), Is.EqualTo(Color.black).Using(ColorEqualityComparer.Instance));

            emitter.enabled = true;
            yield return null;
            Assert.That(ReadEmission(intact), Is.EqualTo(original).Using(ColorEqualityComparer.Instance));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BrokenLampShardsDoNotKeepEmitting()
        {
            GameObject root = new GameObject("Lamp");
            GameObject intact = CreateEmissionVisual(root.transform, "Lamp_Intact");
            GameObject broken = CreateEmissionVisual(root.transform, "Lamp_Broken");
            Light emitter = CreateEmitter(root.transform);
            BreakableLight state = root.AddComponent<BreakableLight>();
            BreakableLightPresenter presenter = root.AddComponent<BreakableLightPresenter>();
            state.Configure(emitter);
            presenter.Configure(state, intact, broken, emitter);

            yield return null;
            Assert.That(ReadEmission(intact).r, Is.GreaterThan(0.1f));

            HitInfo hit = new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 6f);
            Assert.That(state.ReceiveHit(hit), Is.EqualTo(HitResult.Broke));
            Assert.That(ReadEmission(intact), Is.EqualTo(Color.black).Using(ColorEqualityComparer.Instance));
            Assert.That(ReadEmission(broken), Is.EqualTo(Color.black).Using(ColorEqualityComparer.Instance));

            Object.Destroy(root);
            yield return null;
        }

        private static GameObject CreateEmissionVisual(Transform parent, string name)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = "MAT_Lamp_Emissive" };
            material.SetColor(EmissionColorId, new Color(1.9f, 1.76f, 1.4f, 2f));
            visual.GetComponent<Renderer>().sharedMaterial = material;
            return visual;
        }

        private static Light CreateEmitter(Transform parent)
        {
            GameObject emitterObject = new GameObject("PracticalEmitter");
            emitterObject.transform.SetParent(parent, false);
            Light emitter = emitterObject.AddComponent<Light>();
            emitter.color = new Color(1f, 0.86f, 0.69f);
            emitter.intensity = 1f;
            return emitter;
        }

        private static Color ReadEmission(GameObject visual)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            visual.GetComponent<Renderer>().GetPropertyBlock(block, 0);
            return block.GetColor(EmissionColorId);
        }
    }
}
