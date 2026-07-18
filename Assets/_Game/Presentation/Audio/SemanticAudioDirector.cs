using System;
using System.Collections.Generic;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Audio
{
    public enum AudioCueId
    {
        TelevisionPower = 0,
        EntryWarning = 1,
        DoorBreach = 2,
        Gunshot = 3,
        LoopReset = 4,
        TelevisionCrack = 5,
        TelevisionDestroyed = 6,
        EndingSilence = 7
    }

    [Serializable]
    public struct AudioCueBinding
    {
        public AudioCueId Cue;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class SemanticAudioDirector : MonoBehaviour
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private LoopDirector loop;
        [SerializeField] private TelevisionController television;
        [SerializeField] private AudioCueBinding[] bindings;

        private readonly Dictionary<AudioCueId, AudioClip> _fallbacks = new Dictionary<AudioCueId, AudioClip>();
        private AudioSource _source;

        public void Configure(
            GameFlowController flowController,
            LoopDirector loopDirector,
            TelevisionController televisionController)
        {
            flow = flowController;
            loop = loopDirector;
            television = televisionController;
        }

        public void EnsureProceduralBindings()
        {
            AudioCueId[] cues =
            {
                AudioCueId.TelevisionPower, AudioCueId.EntryWarning, AudioCueId.DoorBreach, AudioCueId.Gunshot,
                AudioCueId.LoopReset, AudioCueId.TelevisionCrack, AudioCueId.TelevisionDestroyed,
                AudioCueId.EndingSilence
            };
            bindings = new AudioCueBinding[cues.Length];
            for (int i = 0; i < cues.Length; i++)
            {
                bindings[i] = new AudioCueBinding
                {
                    Cue = cues[i],
                    Clip = GetFallback(cues[i]),
                    Volume = cues[i] == AudioCueId.EndingSilence ? 0.35f : 0.7f
                };
            }
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (flow != null)
            {
                flow.PhaseChanged += OnPhaseChanged;
            }

            if (loop != null)
            {
                loop.EntryWarning += OnEntryWarning;
                loop.LoopResetCompleted += OnLoopReset;
            }

            if (television != null)
            {
                television.DamageChanged += OnTelevisionDamageChanged;
                television.LightImpactPulse += OnTelevisionLightImpact;
            }
        }

        private void Start()
        {
            Play(AudioCueId.TelevisionPower);
        }

        private void OnDisable()
        {
            if (flow != null)
            {
                flow.PhaseChanged -= OnPhaseChanged;
            }

            if (loop != null)
            {
                loop.EntryWarning -= OnEntryWarning;
                loop.LoopResetCompleted -= OnLoopReset;
            }

            if (television != null)
            {
                television.DamageChanged -= OnTelevisionDamageChanged;
                television.LightImpactPulse -= OnTelevisionLightImpact;
            }
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in _fallbacks.Values)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
            }

            _fallbacks.Clear();
        }

        public void Play(AudioCueId cue)
        {
            if (_source == null)
            {
                return;
            }

            AudioClip clip = null;
            float volume = 0.7f;
            if (bindings != null)
            {
                for (int i = 0; i < bindings.Length; i++)
                {
                    if (bindings[i].Cue == cue && bindings[i].Clip != null)
                    {
                        clip = bindings[i].Clip;
                        volume = bindings[i].Volume <= 0f ? 0.7f : bindings[i].Volume;
                        break;
                    }
                }
            }

            if (clip == null || clip.samples <= 0)
            {
                clip = GetFallback(cue);
            }
            if (clip != null)
            {
                _source.PlayOneShot(clip, volume);
            }
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase current, string reason)
        {
            switch (current)
            {
                case GamePhase.Breach:
                    Play(AudioCueId.DoorBreach);
                    break;
                case GamePhase.KillImpact:
                    Play(AudioCueId.Gunshot);
                    break;
                case GamePhase.EndingE1:
                    Play(AudioCueId.TelevisionDestroyed);
                    break;
                case GamePhase.EndingE3:
                    Play(AudioCueId.EndingSilence);
                    break;
            }
        }

        private void OnEntryWarning()
        {
            Play(AudioCueId.EntryWarning);
        }

        private void OnLoopReset(int loopIndex, string outcomeId)
        {
            Play(AudioCueId.LoopReset);
        }

        private void OnTelevisionDamageChanged(TelevisionDamageState state)
        {
            if (state == TelevisionDamageState.Cracked)
            {
                Play(AudioCueId.TelevisionCrack);
            }
        }

        private void OnTelevisionLightImpact()
        {
            Play(AudioCueId.TelevisionCrack);
        }

        private AudioClip GetFallback(AudioCueId cue)
        {
            if (_fallbacks.TryGetValue(cue, out AudioClip existing))
            {
                return existing;
            }

            float frequency = cue switch
            {
                AudioCueId.Gunshot => 95f,
                AudioCueId.DoorBreach => 70f,
                AudioCueId.EntryWarning => 165f,
                AudioCueId.LoopReset => 880f,
                AudioCueId.TelevisionCrack => 1320f,
                AudioCueId.TelevisionDestroyed => 55f,
                AudioCueId.EndingSilence => 40f,
                _ => 330f
            };
            float duration = cue == AudioCueId.EndingSilence ? 0.08f : 0.16f;
            int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - i / (float)sampleCount;
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
                float noise = Mathf.Sin(i * 12.9898f) * 0.18f;
                samples[i] = (tone * 0.35f + noise) * envelope;
            }

            AudioClip generated = AudioClip.Create($"Fallback_{cue}", sampleCount, 1, sampleRate, false);
            generated.SetData(samples, 0);
            _fallbacks.Add(cue, generated);
            return generated;
        }
    }
}
