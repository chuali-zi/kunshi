using ConGame.Domain.Facts;
using ConGame.Runtime.Events;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class RugPullPresenter : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private MeshFilter cloth;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private Animator player;

        private Transform hand;

        private Mesh original;
        private Mesh folded;
        private Vector3[] flatVertices;
        private Vector3[] vertices;
        private Vector3 grip;
        private float pullTime;
        private bool pulling;

        public int ResetOrder => 95;

        public void Configure(MeshFilter surface, GameplayEventHub hub, Animator actor)
        {
            cloth = surface;
            events = hub;
            player = actor;
        }

        private void OnEnable()
        {
            if (events != null) events.KillerStimulusEmitted += OnStimulus;
        }

        private void OnDisable()
        {
            if (events != null) events.KillerStimulusEmitted -= OnStimulus;
            Flatten();
        }

        private void OnDestroy()
        {
            if (folded != null) Destroy(folded);
        }

        private void OnStimulus(KillerStimulus stimulus)
        {
            if (stimulus.Type != KillerStimulusType.TrapPull || stimulus.SourceId != "rug") return;
            if (folded == null)
            {
                original = cloth.sharedMesh;
                folded = Instantiate(original);
                folded.MarkDynamic();
                flatVertices = original.vertices;
                vertices = new Vector3[flatVertices.Length];
            }

            hand = player.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 reach = player.transform.position + player.transform.forward * .48f - player.transform.right * .26f;
            grip = cloth.transform.InverseTransformPoint(reach);
            Bounds bounds = original.bounds;
            grip.x = Mathf.Clamp(grip.x, bounds.min.x, bounds.max.x);
            grip.z = grip.z > bounds.center.z ? bounds.max.z : bounds.min.z;
            grip.y = bounds.max.y;
            cloth.sharedMesh = folded;
            pullTime = Time.time;
            pulling = true;
        }

        private void LateUpdate()
        {
            if (!pulling) return;
            float age = Time.time - pullTime;
            float close = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.49f, .60f, age));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.76f, 1.08f, age));
            Vector3 fingers = cloth.transform.InverseTransformPoint(hand.position - Vector3.up * .075f);
            Vector3 tug = fingers - grip;
            tug.x = Mathf.Clamp(tug.x, -.24f, .24f);
            tug.y = Mathf.Clamp(tug.y, 0f, .36f);
            tug.z = Mathf.Clamp(tug.z, -.20f, .20f);
            tug *= close * (1f - release);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = flatVertices[i];
                float along = (v.x - grip.x) / .64f;
                float across = Mathf.Abs(v.z - grip.z);
                float gather = Mathf.Exp(-along * along - 3f * across);
                vertices[i] = v + tug * gather;
            }
            folded.vertices = vertices;
            folded.RecalculateNormals();
            folded.RecalculateBounds();
            if (release >= 1f) Flatten();
        }

        private void Flatten()
        {
            pulling = false;
            if (original != null && cloth != null) cloth.sharedMesh = original;
        }

        public void CaptureInitialState() { }
        public void Quiesce(in LoopResetContext context) => Flatten();
        public void Restore(in LoopResetContext context) => Flatten();
        public void Resume(in LoopResetContext context) { }
    }
}
