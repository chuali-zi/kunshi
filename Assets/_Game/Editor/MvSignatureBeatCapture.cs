using System;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Presentation.CRT;
using ConGame.Presentation.Vfx;
using ConGame.Runtime.AI;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConGame.Editor
{
    /// <summary>Batch visual acceptance: real scene/events, explicit captures, then observe an actual loop reset.</summary>
    public static class MvSignatureBeatCapture
    {
        const string Pending = "ConGame.MvSignature.Capture";
        static double _deadline;
        static int _step, _epoch;

        // Omit -quit; this method exits after the real reset has completed.
        public static void CaptureGameplay()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Room_M1.unity");
            Application.runInBackground = true;
            SessionState.SetBool(Pending, true);
            Restore();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Restore()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            _deadline = EditorApplication.timeSinceStartup + 120;
            _step = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static T Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        static void Shot(string name) => MvMainShotBuilder.RenderMainCamera("/workspace/ref/mv_round3_" + name + ".png");

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup > _deadline) { Finish(1); return; }
            if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 3f || Time.frameCount < 8) return;
            try
            {
                var flow = Find<GameFlowController>();
                var presenter = Find<MvSignaturePresenter>();
                var loop = Find<LoopDirector>();
                if (_step == 0)
                {
                    Shot("lit");
                    var heavy = new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.up, 10f);
                    foreach (var light in UnityEngine.Object.FindObjectsByType<BreakableLight>(FindObjectsSortMode.None))
                        light.ReceiveHit(heavy);
                    flow.Request(GamePhase.Breach, TransitionPriority.Normal, "MV visual acceptance entry");
                    flow.CommitPending();
                    _step = 1;
                    return;
                }
                if (_step == 1)
                {
                    if (!Find<KillerDirector>().IsLiveHuntActive) return;
                    presenter.Advance(0f);
                    Shot("dark");
                    Find<SemanticVfxDirector>().NotifyShotFired();
                    Shot("muzzle");
                    Debug.Log($"MV_SIGNATURE_LIGHT: enabled={presenter.MuzzleLight.enabled} intensity={presenter.MuzzleLight.intensity} position={presenter.MuzzleLight.transform.position}");
                    Find<TelevisionController>().ReceiveHit(new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 10f));
                    Shot("realisation");
                    presenter.Advance(presenter.Settings.RealisationSeconds);
                    presenter.ReceiveHit(new HitInfo(ImpactKind.Bullet, "pistol_main", Vector3.zero, Vector3.forward, 1f));
                    Shot("glass");
                    loop.BeginConfirmedPerformance();
                    loop.RequestKill("D12");
                    flow.CommitPending();
                    presenter.Advance(0.4f);
                    Shot("title");
                    _epoch = loop.LoopEpoch;
                    _step = 2;
                    return;
                }
                if (loop.LoopEpoch == _epoch || flow.CurrentPhase != GamePhase.Awakening) return;
                if (presenter.TitleAmount != 0f || presenter.ScreenCrackAmount != 0f || presenter.MuzzleLight.enabled)
                    throw new InvalidOperationException("Signature presentation leaked across real reset.");
                Shot("reset");
                Debug.Log($"MV_SIGNATURE_RESET: epoch={loop.LoopEpoch} registered={Find<LoopResetRegistry>().RegisteredCount} title/glass/flash clear.");
                Finish(0);
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
