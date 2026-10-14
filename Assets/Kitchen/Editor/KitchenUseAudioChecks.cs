using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenUseAudioChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            var created = new List<GameObject>();
            KitchenAudioEmitter mouth = null;
            try
            {
                var trashClip = Resources.Load<AudioClip>("KitchenAudio/trashcan");
                var eatingClip = Resources.Load<AudioClip>("KitchenAudio/eating");
                var niceClip = Resources.Load<AudioClip>("KitchenAudio/nice");
                var spewClip = Resources.Load<AudioClip>("KitchenAudio/spew");
                check(trashClip && trashClip.length > 0 && KitchenAudioEmitter.LoadClip(KitchenSound.Trash) == trashClip,
                    "use-audio/trash-recording-loads");
                check(eatingClip && eatingClip.length > 0 && KitchenAudioEmitter.LoadClip(KitchenSound.Eating) == eatingClip,
                    "use-audio/eating-recording-loads");
                check(niceClip && niceClip.length > 0 && KitchenAudioEmitter.LoadClip(KitchenSound.Nice) == niceClip,
                    "use-audio/positive-reaction-recording-loads");
                check(spewClip && spewClip.length > 0 && KitchenAudioEmitter.LoadClip(KitchenSound.Spew) == spewClip,
                    "use-audio/negative-reaction-recording-loads");
                var session = KitchenSession.Instance;
                mouth = KitchenAudioEmitter.Get(session.mouthAnchor ? session.mouthAnchor : session.transform);
                // Earlier gameplay checks also eat meals. Start this timing check with an empty queue.
                mouth.StopAll();
                int eaten = mouth.CueCount(KitchenSound.Eating);
                int nice = mouth.CueCount(KitchenSound.Nice);
                int spew = mouth.CueCount(KitchenSound.Spew);
                var produce = Food("Apple", new Vector3(180, 4, 180), created);
                check(!produce.TryConsume() && mouth.CueCount(KitchenSound.Eating) == eaten
                    && !mouth.EatingSequenceActive && mouth.PendingEatingCueCount == 0,
                    "use-audio/rejected-produce-eating-silent");
                var steak = Food("Steak", new Vector3(180, 4, 182), created);
                steak.ApplyHeat(10f);
                double startedAt = Time.realtimeSinceStartupAsDouble;
                check(steak.TryConsume() && mouth.CueCount(KitchenSound.Eating) == eaten + 1,
                    "use-audio/successful-meal-eating-once");
                check(!steak.TryConsume() && mouth.CueCount(KitchenSound.Eating) == eaten + 1,
                    "use-audio/repeated-consumption-silent");
                var dirtySteak = Food("Steak", new Vector3(180, 4, 184), created);
                dirtySteak.ApplyHeat(10f);
                dirtySteak.MarkUnclean();
                check(dirtySteak.TryConsume() && mouth.CueCount(KitchenSound.Eating) == eaten + 1
                    && mouth.CueCount(KitchenSound.Nice) == nice && mouth.CueCount(KitchenSound.Spew) == spew
                    && mouth.PendingEatingCueCount == 3,
                    "use-audio/second-meal-queued-without-interrupting-first-bite");
                yield return null;
                check(!steak && !dirtySteak && mouth && mouth.OneShotSource && mouth.OneShotSource.clip == eatingClip
                    && mouth.OneShotSource.gameObject.activeInHierarchy,
                    "use-audio/eating-source-survives-food-destruction-at-mouth");

                double reactionStarted = 0;
                yield return AwaitCue(mouth, KitchenSound.Nice, nice + 1, eatingClip, niceClip, startedAt,
                    () => mouth.CueCount(KitchenSound.Eating) == eaten + 1 && mouth.CueCount(KitchenSound.Spew) == spew,
                    lowerBound => reactionStarted = lowerBound, check, "positive-bite-before-nice");
                double secondBiteStarted = 0;
                yield return AwaitCue(mouth, KitchenSound.Eating, eaten + 2, niceClip, eatingClip, reactionStarted,
                    () => mouth.CueCount(KitchenSound.Nice) == nice + 1 && mouth.CueCount(KitchenSound.Spew) == spew,
                    lowerBound => secondBiteStarted = lowerBound, check, "nice-before-second-bite");
                double spewStarted = 0;
                yield return AwaitCue(mouth, KitchenSound.Spew, spew + 1, eatingClip, spewClip, secondBiteStarted,
                    () => mouth.CueCount(KitchenSound.Eating) == eaten + 2 && mouth.CueCount(KitchenSound.Nice) == nice + 1,
                    lowerBound => spewStarted = lowerBound, check, "negative-bite-before-spew");
                double finishDeadline = Time.realtimeSinceStartupAsDouble + spewClip.length + 5;
                while (mouth.EatingSequenceActive && Time.realtimeSinceStartupAsDouble < finishDeadline) yield return null;
                check(!mouth.EatingSequenceActive && mouth.PendingEatingCueCount == 0
                    && Time.realtimeSinceStartupAsDouble + .002 >= spewStarted + spewClip.length
                    && mouth.CueCount(KitchenSound.Eating) == eaten + 2
                    && mouth.CueCount(KitchenSound.Nice) == nice + 1 && mouth.CueCount(KitchenSound.Spew) == spew + 1,
                    "use-audio/two-meals-finish-in-order-with-one-correct-reaction-each");

                // Cancelling a pending sequence must prevent its delayed reaction from leaking into a later task.
                check(mouth.PlayEatingSequence(25) && mouth.PlayEatingSequence(-15),
                    "use-audio/cancellation-fixture-has-current-and-queued-meal");
                mouth.StopAll();
                double cancelledAt = Time.realtimeSinceStartupAsDouble;
                int afterCancelEating = mouth.CueCount(KitchenSound.Eating);
                check(!mouth.EatingSequenceActive && mouth.PendingEatingCueCount == 0
                    && !mouth.OneShotSource.clip && !mouth.LoopSource.clip,
                    "use-audio/stop-clears-current-and-queued-eating-audio");

                var binObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Kitchen/Prefabs/Stations/CompostBin.prefab"), new Vector3(180, 2, 186), Quaternion.identity);
                created.Add(binObject);
                var trash = binObject.GetComponentInChildren<KitchenTrash>();
                var binAudio = KitchenAudioEmitter.Get(trash);
                var trigger = trash.GetComponentsInChildren<Collider>().First(c => c.isTrigger);
                Physics.SyncTransforms();
                int thrown = binAudio.CueCount(KitchenSound.Trash);
                produce.transform.position = trigger.bounds.center;
                produce.SetDesktopHeld(true);
                var body = produce.GetComponent<Rigidbody>();
                body.isKinematic = false;
                body.constraints = RigidbodyConstraints.FreezeAll;
                produce.gameObject.AddComponent<BoxCollider>().size = Vector3.one * .04f;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                check(produce && !produce.IsConsumed && binAudio.CueCount(KitchenSound.Trash) == thrown,
                    "use-audio/held-food-in-bin-silent");
                produce.SetDesktopHeld(false);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                yield return null;
                check(!produce && binAudio.CueCount(KitchenSound.Trash) == thrown + 1
                    && binAudio.OneShotSource.clip == trashClip,
                    "use-audio/released-food-discard-once-despite-multiple-colliders");
                yield return new WaitForFixedUpdate();
                check(binAudio.CueCount(KitchenSound.Trash) == thrown + 1,
                    "use-audio/empty-bin-does-not-repeat-discard-cue");
                var tool = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(tool);
                tool.transform.localScale = Vector3.one * .03f;
                tool.transform.position = trigger.bounds.center;
                var toolBody = tool.AddComponent<Rigidbody>();
                toolBody.useGravity = false;
                toolBody.constraints = RigidbodyConstraints.FreezeAll;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                check(tool && binAudio.CueCount(KitchenSound.Trash) == thrown + 1,
                    "use-audio/non-food-object-not-discarded-and-silent");
                while (Time.realtimeSinceStartupAsDouble < cancelledAt + eatingClip.length + .1f) yield return null;
                check(mouth.CueCount(KitchenSound.Eating) == afterCancelEating
                    && mouth.CueCount(KitchenSound.Nice) == nice + 1 && mouth.CueCount(KitchenSound.Spew) == spew + 1
                    && !mouth.EatingSequenceActive && !mouth.OneShotSource.clip,
                    "use-audio/cancelled-bites-never-start-a-delayed-reaction");
            }
            finally
            {
                if (mouth) mouth.StopAll();
                foreach (GameObject item in created) if (item) Object.Destroy(item);
            }
            yield return null;
        }

        private static IEnumerator AwaitCue(KitchenAudioEmitter mouth, KitchenSound next, int expectedCount,
            AudioClip previousClip, AudioClip nextClip, double previousStartLowerBound, Func<bool> otherCountsUnchanged,
            Action<double> captureStartLowerBound, Action<bool, string> check, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + previousClip.length + 5;
            double lastSampleBeforeStart = previousStartLowerBound;
            bool uninterrupted = true;
            while (mouth.CueCount(next) < expectedCount && Time.realtimeSinceStartupAsDouble < deadline)
            {
                uninterrupted &= mouth.EatingSequenceActive && mouth.OneShotSource.clip == previousClip
                    && !mouth.LoopSource.clip && otherCountsUnchanged();
                lastSampleBeforeStart = Time.realtimeSinceStartupAsDouble;
                yield return null;
            }
            double observedAt = Time.realtimeSinceStartupAsDouble;
            check(uninterrupted && mouth.CueCount(next) == expectedCount && otherCountsUnchanged(),
                "use-audio/" + label + "-uninterrupted-and-in-order");
            check(observedAt + .002 >= previousStartLowerBound + previousClip.length,
                "use-audio/" + label + "-waits-full-recording");
            check(mouth.OneShotSource.clip == nextClip && !mouth.LoopSource.clip,
                "use-audio/" + label + "-uses-single-mouth-voice");
            // A transition occurs between the last sample and this observation. Passing the
            // lower bound avoids treating coroutine scheduling latency as shortened playback.
            captureStartLowerBound(lastSampleBeforeStart);
            Debug.Log("KITCHEN_EATING_SEQUENCE " + label + " elapsedLowerBound="
                + (observedAt - previousStartLowerBound).ToString("F3") + " clip=" + previousClip.length.ToString("F3"));
        }

        private static KitchenFood Food(string kind, Vector3 at, List<GameObject> created)
        {
            var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Kitchen/Prefabs/Food/" + kind + ".prefab"), at, Quaternion.identity);
            created.Add(item);
            foreach (var recovery in item.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            var body = item.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            return item.GetComponent<KitchenFood>();
        }
    }
}
