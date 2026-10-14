using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Kitchen
{
    public enum KitchenSound
    {
        DoorOpen, DoorClose, TapWater, Cooking, Cut, Complete,
        StoveTurn, OvenSpin, ClockTick, OvenDone, LightSwitch, Trash, Eating, Nice, Spew
    }

    /// <summary>Each appliance owns a bounded pair of spatial voices: an action and a loop.</summary>
    [DisallowMultipleComponent]
    public sealed class KitchenAudioEmitter : MonoBehaviour
    {
        private static readonly string[] ClipNames =
        {
            "door-open", "door-close", "tap-water", "cooking", "cut", "complete",
            "stove-turning-on-96102", "oven-spin", "clock-ticking-down", "oven-done", "light-switch-382712",
            "trashcan", "eating", "nice", "spew"
        };
        private static readonly float[] Gains = { .24f, .5f, .55f, .65f, .6f, .5f, .5f, .85f, .65f, .5f, .55f, .55f, .55f, .55f, .55f };
        private static readonly Dictionary<KitchenSound, AudioClip> Clips = new Dictionary<KitchenSound, AudioClip>();
        private static readonly HashSet<KitchenSound> Missing = new HashSet<KitchenSound>();
        private readonly Dictionary<KitchenSound, float> lastCueTimes = new Dictionary<KitchenSound, float>();
        private readonly Dictionary<KitchenSound, int> cueCounts = new Dictionary<KitchenSound, int>();
        private readonly Dictionary<KitchenSound, int> loopCounts = new Dictionary<KitchenSound, int>();
        private readonly Queue<KitchenSound> eatingCues = new Queue<KitchenSound>();
        private KitchenSound? currentCue;
        private KitchenSound? currentLoop;
        private double eatingCueEarliestEnd;

        public AudioSource OneShotSource { get; private set; }
        public AudioSource LoopSource { get; private set; }
        public bool EatingSequenceActive { get; private set; }
        public int PendingEatingCueCount => eatingCues.Count;
        public int CueCount(KitchenSound sound) => cueCounts.TryGetValue(sound, out int count) ? count : 0;
        public int LoopStartCount(KitchenSound sound) => loopCounts.TryGetValue(sound, out int count) ? count : 0;

        public static KitchenAudioEmitter Get(Component owner)
        {
            if (!owner) return null;
            var emitter = owner.GetComponent<KitchenAudioEmitter>();
            return emitter ? emitter : owner.gameObject.AddComponent<KitchenAudioEmitter>();
        }

        private void Awake()
        {
            OneShotSource = MakeSource("Action audio", false);
            LoopSource = MakeSource("Continuous audio", true);
        }

        private AudioSource MakeSource(string objectName, bool loop)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = .65f;
            source.maxDistance = 9f;
            source.priority = loop ? 160 : 100;
            return source;
        }

        public static AudioClip LoadClip(KitchenSound sound)
        {
            if (Clips.TryGetValue(sound, out var clip) && clip) return clip;
            clip = Resources.Load<AudioClip>("KitchenAudio/" + ClipNames[(int)sound]);
            if (clip) Clips[sound] = clip;
            else if (Missing.Add(sound)) Debug.LogError("Missing kitchen audio: " + ClipNames[(int)sound]);
            return clip;
        }

        public bool PlayCue(KitchenSound sound, float volume = 1f, float minInterval = 0f)
        {
            if (!isActiveAndEnabled) return false;
            if (minInterval > 0f && lastCueTimes.TryGetValue(sound, out float last)
                && (Time.unscaledTime - last < minInterval || (currentCue == sound && OneShotSource.isPlaying))) return false;
            AudioClip clip = LoadClip(sound);
            if (!clip) return false;
            CancelEatingSequence();
            StartCue(sound, clip, volume);
            return true;
        }

        private void StartCue(KitchenSound sound, AudioClip clip, float volume)
        {
            // A new action replaces the preceding action on this same object. Turning
            // a knob every frame cannot stack hundreds of overlapping recordings.
            OneShotSource.Stop();
            OneShotSource.clip = clip;
            OneShotSource.volume = Mathf.Clamp01(volume * Gains[(int)sound]);
            if (sound == KitchenSound.Cut && clip.length > .25f) OneShotSource.time = .22f;
            else OneShotSource.time = 0f;
            currentCue = sound;
            lastCueTimes[sound] = Time.unscaledTime;
            cueCounts[sound] = CueCount(sound) + 1;
            OneShotSource.Play();
        }

        /// <summary>Keep each successful bite and its reaction together, even after its food is destroyed.</summary>
        public bool PlayEatingSequence(int energyChange)
        {
            if (!isActiveAndEnabled || !LoadClip(KitchenSound.Eating)) return false;
            KitchenSound? reaction = energyChange > 0 ? KitchenSound.Nice
                : energyChange < 0 ? KitchenSound.Spew : (KitchenSound?)null;
            if (reaction.HasValue && !LoadClip(reaction.Value)) return false;

            eatingCues.Enqueue(KitchenSound.Eating);
            if (reaction.HasValue) eatingCues.Enqueue(reaction.Value);
            if (!EatingSequenceActive) StartNextEatingCue();
            return true;
        }

        private void StartNextEatingCue()
        {
            while (eatingCues.Count > 0)
            {
                KitchenSound sound = eatingCues.Dequeue();
                AudioClip clip = LoadClip(sound);
                if (!clip) continue;
                StartCue(sound, clip, 1f);
                EatingSequenceActive = true;
                // isPlaying may be false with audio disabled or a headless test runner.
                // Still wait the entire recording; with real output also wait for the
                // source to finish, so audio-buffer startup cannot truncate the bite.
                eatingCueEarliestEnd = Time.realtimeSinceStartupAsDouble
                    + clip.length / Mathf.Max(.01f, Mathf.Abs(OneShotSource.pitch));
                return;
            }
            EatingSequenceActive = false;
        }

        private void Update()
        {
            if (!EatingSequenceActive || !OneShotSource) return;
            if (AudioListener.pause && !OneShotSource.ignoreListenerPause) return;
            if (Time.realtimeSinceStartupAsDouble < eatingCueEarliestEnd || OneShotSource.isPlaying) return;
            StartNextEatingCue();
        }

        private void CancelEatingSequence()
        {
            eatingCues.Clear();
            EatingSequenceActive = false;
            eatingCueEarliestEnd = 0;
        }

        public void SetLoop(KitchenSound sound, bool active, float volume = 1f)
        {
            if (!active || !isActiveAndEnabled)
            {
                if (currentLoop == sound) StopLoop();
                return;
            }
            AudioClip clip = LoadClip(sound);
            if (!clip) return;
            LoopSource.volume = Mathf.Clamp01(volume * Gains[(int)sound]);
            // State, rather than isPlaying, controls starts. This is stable through
            // audio virtualization and never restarts a loop in every gameplay Tick.
            if (currentLoop == sound && LoopSource.clip == clip) return;
            StopLoop();
            currentLoop = sound;
            LoopSource.clip = clip;
            loopCounts[sound] = LoopStartCount(sound) + 1;
            LoopSource.Play();
        }

        private void StopLoop()
        {
            if (LoopSource) { LoopSource.Stop(); LoopSource.clip = null; }
            currentLoop = null;
        }

        public void StopAll()
        {
            CancelEatingSequence();
            StopLoop();
            if (OneShotSource) { OneShotSource.Stop(); OneShotSource.clip = null; }
            currentCue = null;
        }

        private void OnDisable() => StopAll();
    }
}
