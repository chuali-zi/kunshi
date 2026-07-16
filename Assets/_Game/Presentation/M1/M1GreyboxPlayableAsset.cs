using System;
using ConGame.Domain.Counterplay;
using UnityEngine;
using UnityEngine.Playables;

namespace ConGame.Presentation.M1
{
    public enum M1GreyboxDeathStyle
    {
        DefaultShot = 0,
        SofaRearShot = 1,
        ArmedStandoff = 2
    }

    [CreateAssetMenu(menuName = "ConGame/M1/Greybox Performance", fileName = "PERF_M1_New")]
    public sealed class M1GreyboxPlayableAsset : PlayableAsset
    {
        [SerializeField] private M1GreyboxDeathStyle style;
        [SerializeField, Min(0.25f)] private float sequenceDuration = 1.6f;
        [SerializeField] private string outcomeId;
        [SerializeField] private PerformanceHeldItemPolicy heldItemPolicy;
        [SerializeField] private QteWindowDefinition[] qteWindows = Array.Empty<QteWindowDefinition>();
        [SerializeField] private PerformanceBranchDefinition[] resultBranches =
            Array.Empty<PerformanceBranchDefinition>();

        public M1GreyboxDeathStyle Style => style;
        public string OutcomeId => outcomeId ?? string.Empty;
        public PerformanceHeldItemPolicy HeldItemPolicy => heldItemPolicy;
        public QteWindowDefinition[] QteWindows => qteWindows ?? Array.Empty<QteWindowDefinition>();
        public PerformanceBranchDefinition[] ResultBranches =>
            resultBranches ?? Array.Empty<PerformanceBranchDefinition>();
        public float SequenceDuration => Mathf.Max(0.25f, sequenceDuration);

        public float TotalQteDuration
        {
            get
            {
                float total = 0f;
                QteWindowDefinition[] windows = QteWindows;
                for (int i = 0; i < windows.Length; i++)
                {
                    total += windows[i].SafeDurationSeconds;
                }

                return total;
            }
        }

        public override double duration => Math.Max(SequenceDuration, TotalQteDuration);

        // Kept for existing M1 assets and callers. New content should use the full overload.
        public void Configure(M1GreyboxDeathStyle deathStyle, float seconds)
        {
            style = deathStyle;
            sequenceDuration = Mathf.Max(0.25f, seconds);
        }

        public void Configure(
            string id,
            M1GreyboxDeathStyle deathStyle,
            float seconds,
            PerformanceHeldItemPolicy itemPolicy,
            QteWindowDefinition[] windows,
            PerformanceBranchDefinition[] branches)
        {
            outcomeId = id ?? string.Empty;
            style = deathStyle;
            sequenceDuration = Mathf.Max(0.25f, seconds);
            heldItemPolicy = itemPolicy;
            qteWindows = CopyWindows(windows);
            resultBranches = CopyBranches(branches);
        }

        public bool TryResolveBranch(
            bool allQteWindowsSucceeded,
            out PerformanceBranchDefinition branch)
        {
            PerformanceBranchDefinition[] branches = ResultBranches;
            for (int i = 0; i < branches.Length; i++)
            {
                if (branches[i].RequiresAllQteWindows == allQteWindowsSucceeded)
                {
                    branch = branches[i];
                    return true;
                }
            }

            branch = default;
            return false;
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return Playable.Create(graph);
        }

        private static QteWindowDefinition[] CopyWindows(QteWindowDefinition[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<QteWindowDefinition>();
            }

            QteWindowDefinition[] copy = new QteWindowDefinition[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static PerformanceBranchDefinition[] CopyBranches(PerformanceBranchDefinition[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<PerformanceBranchDefinition>();
            }

            PerformanceBranchDefinition[] copy = new PerformanceBranchDefinition[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
