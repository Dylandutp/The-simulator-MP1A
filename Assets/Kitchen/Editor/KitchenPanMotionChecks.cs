using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Moves the held pan itself; these checks never invoke the keyboard toss helper.</summary>
    public static class KitchenPanMotionChecks
    {
        static readonly Vector3 Origin = new Vector3(120f, 4f, 120f);

        sealed class Fixture
        {
            public KitchenPan pan;
            public Rigidbody panBody;
            public KitchenDesktopHeld held;
            public KitchenFood steak;
            public Rigidbody steakBody;
            public GameObject support;

            public void Dispose()
            {
                if (steak) Object.Destroy(steak.gameObject);
                if (pan) Object.Destroy(pan.gameObject);
                if (support) Object.Destroy(support);
            }
        }

        public static IEnumerator Run(Action<bool, string> check)
        {
            yield return GentleMotionAndRelease(check);
            yield return ForgivingRapidMotion(check);
            yield return ExcessiveSidewaysMotion(check);
        }

        static Fixture Create(Vector3 origin)
        {
            var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/InductionHob.prefab");
            var sourcePan = station.GetComponentInChildren<KitchenPan>();
            var fixture = new Fixture();
            fixture.pan = Object.Instantiate(sourcePan.gameObject, origin, Quaternion.identity).GetComponent<KitchenPan>();
            var panRecovery = fixture.pan.GetComponent<KitchenRespawnTool>();
            if (panRecovery) panRecovery.enabled = false;
            fixture.panBody = fixture.pan.GetComponent<Rigidbody>();
            fixture.panBody.isKinematic = true;
            fixture.panBody.useGravity = false;
            fixture.panBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            fixture.held = fixture.pan.GetComponent<KitchenDesktopHeld>();
            if (!fixture.held) fixture.held = fixture.pan.gameObject.AddComponent<KitchenDesktopHeld>();
            fixture.held.SetHeld(true);
            var steakPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/Steak.prefab");
            Vector3 seat = SeatPosition(fixture);
            fixture.steak = Object.Instantiate(steakPrefab, seat, Quaternion.identity).GetComponent<KitchenFood>();
            var recovery = fixture.steak.GetComponent<KitchenRespawnTool>();
            if (recovery) recovery.enabled = false;
            fixture.steakBody = fixture.steak.GetComponent<Rigidbody>();
            fixture.steakBody.isKinematic = false;
            fixture.steakBody.useGravity = true;
            fixture.steakBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            fixture.steakBody.linearVelocity = Vector3.zero;
            fixture.steakBody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            return fixture;
        }

        static Vector3 SeatPosition(Fixture fixture) => fixture.pan.cookingSurface.TransformPoint(
            new Vector3(0f, fixture.pan.supportedLocalHeight, 0f));

        static void MovePan(Fixture fixture, Vector3 delta)
        {
            Vector3 target = fixture.panBody.position + delta;
            fixture.panBody.position = target;
            fixture.pan.transform.position = target;
            Physics.SyncTransforms();
        }

        static IEnumerator WarmUp()
        {
            float until = Time.time + .42f;
            while (Time.time < until) yield return new WaitForFixedUpdate();
        }

        static bool PairsAre(Fixture fixture, bool expected)
        {
            foreach (var panCollider in fixture.pan.GetComponentsInChildren<Collider>())
            foreach (var foodCollider in fixture.steak.GetComponentsInChildren<Collider>())
            {
                if (panCollider.isTrigger || foodCollider.isTrigger || !panCollider.enabled || !foodCollider.enabled
                    || panCollider.attachedRigidbody != fixture.panBody || foodCollider.attachedRigidbody != fixture.steakBody) continue;
                if (Physics.GetIgnoreCollision(panCollider, foodCollider) != expected) return false;
            }
            return true;
        }

        static IEnumerator GentleMotionAndRelease(Action<bool, string> check)
        {
            var fixture = Create(Origin);
            try
            {
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                check(fixture.pan.SupportedSteakCount == 1 && PairsAre(fixture, true), "pan-motion/held-steak-has-contact-assistance");
                // A fast initial pickup is suppressed, rather than mistaken for an intentional fling.
                MovePan(fixture, Vector3.right * (4f * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                check(fixture.pan.TossCount == 0 && fixture.pan.EscapedSteakCount == 0 && !fixture.steak.IsDirty,
                    "pan-motion/pickup-grace-does-not-spill");
                yield return WarmUp();

                // Ordinary hand travel stays below the deliberate sideways yank threshold.
                for (int i = 0; i < 8; i++)
                {
                    MovePan(fixture, Vector3.right * (1.6f * Time.fixedDeltaTime));
                    yield return new WaitForFixedUpdate();
                }
                yield return new WaitForFixedUpdate();
                check(fixture.pan.TossCount == 0 && fixture.pan.SupportedSteakCount == 1 && !fixture.steak.IsDirty,
                    "pan-motion/slow-horizontal-carry-does-not-toss");

                MovePan(fixture, Vector3.up * Time.fixedDeltaTime);
                Vector3 seat = SeatPosition(fixture);
                yield return new WaitForFixedUpdate();
                check(fixture.pan.TossCount == 1 && fixture.pan.IsTossing, "pan-motion/real-upward-flick-launches-steak");
                float highest = fixture.steakBody.position.y;
                float drift = 0f;
                float until = Time.time + 1f;
                while (fixture.pan.IsTossing && Time.time < until)
                {
                    yield return new WaitForFixedUpdate();
                    highest = Mathf.Max(highest, fixture.steakBody.position.y);
                    drift = Mathf.Max(drift, Vector3.ProjectOnPlane(fixture.steakBody.position - seat, Vector3.up).magnitude);
                }
                check(highest - seat.y > .025f && highest - seat.y < .25f && drift < .25f,
                    "pan-motion/natural-toss-height-and-drift-bounded");
                check(fixture.pan.CatchCount == 1 && !fixture.pan.IsTossing && fixture.pan.SupportedSteakCount == 1
                    && fixture.pan.EscapedSteakCount == 0 && !fixture.steak.IsDirty,
                    "pan-motion/natural-toss-catches-clean-steak");
                check(!fixture.steakBody.isKinematic && fixture.steakBody.useGravity && !fixture.steak.IsHeld,
                    "pan-motion/support-retains-food-gravity-and-grab-flags");

                // A physical worktop supports the released dynamic pan. Neither utensil nor food is frozen.
                fixture.support = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fixture.support.name = "Pan motion check worktop";
                fixture.support.transform.position = fixture.panBody.position + Vector3.down * .029f;
                fixture.support.transform.localScale = new Vector3(1.2f, .05f, 1.2f);
                fixture.held.SetHeld(false);
                fixture.panBody.isKinematic = false;
                fixture.panBody.useGravity = true;
                fixture.panBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                check(PairsAre(fixture, false) && fixture.pan.SupportedSteakCount == 0,
                    "pan-motion/releasing-pan-restores-physical-contact");
                yield return new WaitForSeconds(.3f);
                check(!fixture.steakBody.isKinematic && fixture.steakBody.useGravity && !fixture.steak.IsHeld
                    && !fixture.panBody.isKinematic && fixture.panBody.useGravity,
                    "pan-motion/released-food-and-pan-remain-dynamic");
                check(Vector3.Distance(fixture.steakBody.position, SeatPosition(fixture)) < .15f
                    && fixture.steakBody.linearVelocity.magnitude < .65f && !fixture.steak.IsDirty,
                    "pan-motion/released-pan-keeps-steak-without-contact-explosion");
                Debug.Log("KITCHEN_PAN_MOTION: naturalPeak=" + (highest - seat.y).ToString("0.000") + " drift=" + drift.ToString("0.000"));
            }
            finally { fixture.Dispose(); }
            yield return null;
        }

        static IEnumerator ExcessiveSidewaysMotion(Action<bool, string> check)
        {
            var fixture = Create(Origin + Vector3.right * 2f);
            try
            {
                yield return WarmUp();
                check(fixture.pan.SupportedSteakCount == 1 && !fixture.steak.IsDirty, "pan-motion/sideways-test-starts-supported-clean");
                for (int i = 0; i < 5; i++)
                {
                    MovePan(fixture, Vector3.right * (5f * Time.fixedDeltaTime));
                    yield return new WaitForFixedUpdate();
                }
                check(fixture.pan.EscapedSteakCount == 1 && fixture.pan.SupportedSteakCount == 0 && fixture.steak.IsDirty,
                    "pan-motion/sustained-excessive-side-yank-spills-and-contaminates");
                yield return new WaitForSeconds(.18f);
                check(Vector3.ProjectOnPlane(fixture.steakBody.position - SeatPosition(fixture), Vector3.up).magnitude > .2f,
                    "pan-motion/side-yank-physically-clears-the-pan");
                check(!fixture.steakBody.isKinematic && fixture.steakBody.useGravity && PairsAre(fixture, false),
                    "pan-motion/spilled-steak-retains-free-physics-and-restored-contact");
            }
            finally { fixture.Dispose(); }
            yield return null;
        }

        static IEnumerator ForgivingRapidMotion(Action<bool, string> check)
        {
            var fixture = Create(Origin + Vector3.right * 4f);
            try
            {
                yield return WarmUp();
                MovePan(fixture, Vector3.right * (5.2f * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                check(fixture.pan.SupportedSteakCount == 1 && fixture.pan.EscapedSteakCount == 0 && !fixture.steak.IsDirty,
                    "pan-motion/single-fast-tracking-spike-does-not-spill");

                // This used to exceed the single-frame upward spill threshold. It is
                // now an ordinary bounded toss, even while the hand keeps moving sideways.
                MovePan(fixture, Vector3.up * (2.9f * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
                check(fixture.pan.IsTossing && fixture.pan.EscapedSteakCount == 0,
                    "pan-motion/brisk-upward-flick-starts-forgiving-toss");
                float largestDrift = 0f;
                for (int i = 0; i < 12; i++)
                {
                    MovePan(fixture, Vector3.right * (3f * Time.fixedDeltaTime));
                    yield return new WaitForFixedUpdate();
                    largestDrift = Mathf.Max(largestDrift,
                        Vector3.ProjectOnPlane(fixture.steakBody.position - SeatPosition(fixture), Vector3.up).magnitude);
                }
                float until = Time.time + .85f;
                while (fixture.pan.IsTossing && Time.time < until) yield return new WaitForFixedUpdate();
                check(largestDrift < .34f && fixture.pan.CatchCount >= 1 && fixture.pan.SupportedSteakCount == 1,
                    "pan-motion/brisk-moving-pan-catches-toss-with-limited-drift");
                check(!fixture.steak.IsDirty && fixture.pan.EscapedSteakCount == 0,
                    "pan-motion/normal-fast-toss-and-travel-preserve-clean-steak");
            }
            finally { fixture.Dispose(); }
            yield return null;
        }
    }
}
