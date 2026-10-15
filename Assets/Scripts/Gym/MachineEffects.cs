using System.Collections;
using UnityEngine;

namespace HouseSim.Gym
{
    public sealed class MachineEffects : MonoBehaviour
    {
        [SerializeField] AudioSource loopSource;
        [SerializeField] ParticleSystem effectParticles;
        [SerializeField] AudioClip sessionClip;

        Coroutine timedEffect;

        public AudioClip SessionClip => sessionClip;

        public bool IsPlaying =>
            (loopSource != null && loopSource.isPlaying) ||
            (effectParticles != null && effectParticles.IsAlive(true));

        public void PlaySession()
        {
            if (effectParticles != null) effectParticles.Play();
            if (loopSource == null || loopSource.isPlaying) return;

            loopSource.loop = true;
            if (sessionClip != null) loopSource.clip = sessionClip;
            loopSource.Play();
        }

        public void StopSession()
        {
            if (effectParticles != null) effectParticles.Stop();
            if (loopSource != null) loopSource.Stop();
        }

        public void PlayOneShot(AudioClip clip)
        {
            if (loopSource != null && clip != null) loopSource.PlayOneShot(clip);
        }

        public void PlayTimedEffect(AudioClip clip, float duration)
        {
            if (effectParticles != null) effectParticles.Play();
            PlayOneShot(clip != null ? clip : sessionClip);

            if (timedEffect != null) StopCoroutine(timedEffect);
            timedEffect = StartCoroutine(StopTimedEffect(Mathf.Max(0f, duration)));
        }

        IEnumerator StopTimedEffect(float duration)
        {
            if (duration > 0f) yield return new WaitForSeconds(duration);
            if (effectParticles != null) effectParticles.Stop();
            if (loopSource != null && loopSource.isPlaying) loopSource.Stop();
            timedEffect = null;
        }
    }
}
