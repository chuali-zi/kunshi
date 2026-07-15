using System;
using ConGame.Runtime.Counterplay;
using UnityEngine;
using UnityEngine.Playables;

namespace ConGame.Presentation.Timeline
{

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class TimelineSequenceRunner : MonoBehaviour, IPerformancePlayer
    {
        [SerializeField] private PlayableDirector director;

        private Action _completed;

        private void Awake()
        {
            director ??= GetComponent<PlayableDirector>();
        }

        private void OnEnable()
        {
            if (director != null)
            {
                director.stopped += OnDirectorStopped;
            }
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.stopped -= OnDirectorStopped;
            }

            _completed = null;
        }

        public bool Play(CounterplayDefinitionAsset definition, Action completed)
        {
            if (director == null || definition == null || definition.Performance == null)
            {
                return false;
            }

            _completed = completed;
            director.playableAsset = definition.Performance;
            director.time = 0d;
            director.Play();
            return true;
        }

        public void CancelAllForLoop(int loopEpoch)
        {
            _completed = null;
            if (director != null && director.state == PlayState.Playing)
            {
                director.Stop();
            }
        }

        private void OnDirectorStopped(PlayableDirector stoppedDirector)
        {
            Action callback = _completed;
            _completed = null;
            callback?.Invoke();
        }
    }
}
