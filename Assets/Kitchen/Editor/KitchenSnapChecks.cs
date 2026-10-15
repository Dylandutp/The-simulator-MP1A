using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Exercises real release callbacks and physics frames without moving the authored oven.</summary>
    public static class KitchenSnapChecks
    {
        public static IEnumerator Run(Action<bool, string> assert)
        {
            var spawned = new List<GameObject>();
            try
            {
                assert(Application.isPlaying && KitchenSession.Instance,
                    "oven-snap/runs-with-live-session");
                GameObject ovenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/Oven.prefab");
                assert(ovenPrefab, "oven-snap/uses-saved-oven-prefab");
                GameObject station = Object.Instantiate(ovenPrefab, new Vector3(80f, 1f, 80f), Quaternion.Euler(0f, 37f, 0f));
                spawned.Add(station);
                KitchenOven oven = station.GetComponent<KitchenOven>();
                assert(oven && oven.chamber && oven.door && oven.timerKnob,
                    "oven-snap/isolated-prefab-configured");
                oven.timerKnob.SetValue(0f);
                oven.door.SetOpenImmediately(true);
                KitchenFood chicken = Spawn("Chicken", oven, new Vector3(0.12f, -0.04f, -0.29f), spawned);
                Rigidbody body = chicken.GetComponent<Rigidbody>();
                Vector3 initialPosition = body.position;
                float waitUntil = Time.fixedTime + 0.08f;
                int afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Vector3.Distance(body.position, initialPosition) < 0.005f,
                    "oven-snap/nearby-unheld-chicken-not-pulled-in");

                // Match the desktop release ordering: mark the food released while its
                // body is still kinematic, then restore dynamics and the release velocity.
                body.rotation = oven.chamber.rotation * Quaternion.Euler(25f, 15f, 8f);
                chicken.transform.rotation = body.rotation;
                Hold(chicken);
                chicken.SetDesktopHeld(false);
                assert(Vector3.Distance(body.position, initialPosition) < 0.005f,
                    "oven-snap/selection-callback-defers-physics-placement");
                RestoreReleasedBody(chicken);
                body.linearVelocity = oven.chamber.right * 0.03f + Vector3.up * 0.04f;
                body.angularVelocity = new Vector3(0.12f, 0.04f, 0.03f);
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                Vector3 snappedLocal = oven.chamber.InverseTransformPoint(body.position);
                assert(Mathf.Abs(snappedLocal.x) < 0.005f && Mathf.Abs(snappedLocal.z) < 0.005f,
                    "oven-snap/release-centers-chicken-on-rack");
                assert(new Bounds(Vector3.zero, oven.chamberHalfExtents * 2f).Contains(snappedLocal),
                    "oven-snap/placed-root-within-baking-detection");
                assert(Quaternion.Angle(body.rotation, oven.chamber.rotation) < 0.1f,
                    "oven-snap/tilted-chicken-becomes-upright");
                assert(body.linearVelocity.sqrMagnitude < 0.000001f && body.angularVelocity.sqrMagnitude < 0.000001f,
                    "oven-snap/clears-release-linear-and-angular-velocity");
                Physics.SyncTransforms();
                BoxCollider rack = oven.transform.Find("Rack").GetComponent<BoxCollider>();
                float bottomGap = chicken.GetComponent<BoxCollider>().bounds.min.y - rack.bounds.max.y;
                assert(bottomGap >= -0.002f && bottomGap <= 0.01f,
                    "oven-snap/rests-above-rack-without-penetration");

                Vector3 occupiedPosition = body.position;
                KitchenFood second = Spawn("Chicken", oven, new Vector3(0f, 0.13f, -0.30f), spawned);
                Vector3 secondPosition = second.transform.position;
                Hold(second);
                Release(second);
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Vector3.Distance(second.transform.position, secondPosition) < 0.01f
                    && Vector3.Distance(body.position, occupiedPosition) < 0.005f,
                    "oven-snap/occupied-slot-does-not-stack-or-move-chickens");
                second.gameObject.SetActive(false);

                // Placement is a one-shot aid, not a permanent magnet holding the chicken.
                Vector3 displaced = oven.chamber.TransformPoint(new Vector3(0.1f, 0.08f, -0.2f));
                body.position = displaced;
                chicken.transform.position = displaced;
                body.WakeUp();
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Vector3.Distance(body.position, displaced) < 0.005f,
                    "oven-snap/successful-release-is-consumed-once");
                chicken.gameObject.SetActive(false);

                float doorPlane = oven.chamber.InverseTransformPoint(oven.door.Pivot.position).z;
                // Exact regression: root at the door plane, roughly half the chicken
                // inside the mouth. A root-only/deep-chamber test rejects this placement.
                KitchenFood mouthChicken = Spawn("Chicken", oven, new Vector3(0f, -0.12f, doorPlane), spawned);
                Rigidbody mouthBody = mouthChicken.GetComponent<Rigidbody>();
                Vector3 mouthPosition = mouthChicken.transform.position;
                assert(!oven.IsInsideSnapRange(mouthPosition) && oven.IsInsideSnapRange(mouthChicken),
                    "oven-snap/half-in-mouth-overlap-qualifies-with-root-at-door");
                Hold(mouthChicken);
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Vector3.Distance(mouthBody.position, mouthPosition) < 0.005f,
                    "oven-snap/half-in-held-chicken-does-not-snap");
                mouthBody.useGravity = true;
                Release(mouthChicken);
                waitUntil = Time.fixedTime + 0.14f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                Vector3 mouthPlaced = oven.chamber.InverseTransformPoint(mouthBody.position);
                assert(Mathf.Abs(mouthPlaced.x) < 0.01f && Mathf.Abs(mouthPlaced.z) < 0.01f
                    && new Bounds(Vector3.zero, oven.chamberHalfExtents * 2f).Contains(mouthPlaced),
                    "oven-snap/stationary-half-in-release-snaps-and-settles-on-rack");
                assert(Quaternion.Angle(mouthBody.rotation, oven.chamber.rotation) < 1f,
                    "oven-snap/mouth-release-settles-upright-under-gravity");
                mouthChicken.gameObject.SetActive(false);

                // Entire collider outside, unlike the legitimate half-in doorway case.
                Vector3 outside = oven.chamber.TransformPoint(new Vector3(0f, 0.08f, doorPlane - 0.35f));
                assert(!oven.IsInsideSnapRange(outside)
                    && !oven.IsInsideSnapRange(oven.chamber.TransformPoint(new Vector3(0f, 0f, doorPlane - 0.001f))),
                    "oven-snap/capture-never-extends-outside-door-plane");
                KitchenFood outsideChicken = Spawn("Chicken", oven, new Vector3(0f, 0.08f, doorPlane - 0.35f), spawned);
                assert(!oven.IsInsideSnapRange(outsideChicken),
                    "oven-snap/entirely-outside-food-has-no-entry-volume-overlap");
                Hold(outsideChicken);
                Release(outsideChicken);
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Vector3.Distance(outsideChicken.transform.position, outside) < 0.005f,
                    "oven-snap/release-outside-door-not-teleported");
                outsideChicken.gameObject.SetActive(false);

                KitchenFood sideChicken = Spawn("Chicken", oven,
                    new Vector3(oven.chamberHalfExtents.x + 0.2f, -0.02f, -0.12f), spawned);
                assert(!oven.IsInsideSnapRange(sideChicken),
                    "oven-snap/collider-crossing-sidewall-is-not-entry-through-mouth");
                Hold(sideChicken);
                Release(sideChicken);
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Mathf.Abs(oven.chamber.InverseTransformPoint(sideChicken.transform.position).x) > 0.4f,
                    "oven-snap/release-beside-oven-never-snaps-through-sidewall");
                sideChicken.gameObject.SetActive(false);

                oven.door.SetOpenImmediately(false);
                KitchenFood closedChicken = Spawn("Chicken", oven, new Vector3(0.1f, 0.05f, 0f), spawned);
                Vector3 closedPosition = closedChicken.transform.position;
                Hold(closedChicken);
                Release(closedChicken);
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Vector3.Distance(closedChicken.transform.position, closedPosition) < 0.005f,
                    "oven-snap/closed-door-blocks-release-assist");
                oven.door.SetOpenImmediately(true);
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Vector3.Distance(closedChicken.transform.position, closedPosition) < 0.005f,
                    "oven-snap/opening-door-does-not-replay-old-release");
                closedChicken.gameObject.SetActive(false);

                KitchenFood steak = Spawn("Steak", oven, new Vector3(0.1f, 0.05f, -0.2f), spawned);
                Vector3 steakPosition = steak.transform.position;
                Hold(steak);
                Release(steak);
                waitUntil = Time.fixedTime + 0.08f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                assert(Vector3.Distance(steak.transform.position, steakPosition) < 0.005f,
                    "oven-snap/does-not-capture-other-food-types");

                GameObject scaledStation = Object.Instantiate(ovenPrefab,
                    new Vector3(85f, 1f, 80f), Quaternion.Euler(0f, -63f, 0f));
                spawned.Add(scaledStation);
                scaledStation.transform.localScale = new Vector3(1.1f, 0.9f, 1.2f);
                KitchenOven scaledOven = scaledStation.GetComponent<KitchenOven>();
                scaledOven.timerKnob.SetValue(0f);
                scaledOven.door.SetOpenImmediately(true);
                float scaledDoorPlane = scaledOven.chamber.InverseTransformPoint(scaledOven.door.Pivot.position).z;
                KitchenFood scaledMouthChicken = Spawn("Chicken", scaledOven,
                    new Vector3(0f, -0.12f, scaledDoorPlane), spawned);
                assert(scaledOven.IsInsideSnapRange(scaledMouthChicken),
                    "oven-snap/rotated-nonuniform-oven-recognizes-mouth-overlap");
                Hold(scaledMouthChicken);
                Release(scaledMouthChicken);
                waitUntil = Time.fixedTime + 0.1f;
                afterFrame = Time.frameCount + 2;
                while (Time.fixedTime < waitUntil || Time.frameCount < afterFrame) yield return null;
                Vector3 scaledPlaced = scaledOven.chamber.InverseTransformPoint(scaledMouthChicken.transform.position);
                assert(Mathf.Abs(scaledPlaced.x) < 0.005f && Mathf.Abs(scaledPlaced.z) < 0.005f
                    && new Bounds(Vector3.zero, scaledOven.chamberHalfExtents * 2f).Contains(scaledPlaced)
                    && Quaternion.Angle(scaledMouthChicken.transform.rotation, scaledOven.chamber.rotation) < 0.1f,
                    "oven-snap/rotated-nonuniform-oven-places-half-in-chicken-correctly");
                assert(Mathf.Approximately(chicken.CookSeconds, 0f) && Mathf.Approximately(oven.RemainingSeconds, 0f),
                    "oven-snap/placement-checks-do-not-start-cooking");
            }
            finally
            {
                // Disable immediately to remove colliders and static event listeners;
                // normal destruction then releases any claimed inventory portions.
                foreach (GameObject created in spawned)
                {
                    if (!created) continue;
                    created.SetActive(false);
                    Object.Destroy(created);
                }
            }
        }

        private static KitchenFood Spawn(string name, KitchenOven oven, Vector3 local, List<GameObject> spawned)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/" + name + ".prefab");
            GameObject item = Object.Instantiate(prefab, oven.chamber.TransformPoint(local), oven.chamber.rotation);
            spawned.Add(item);
            foreach (KitchenRespawnTool recovery in item.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            Rigidbody body = item.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            item.AddComponent<KitchenDesktopHeld>();
            Physics.SyncTransforms();
            return item.GetComponent<KitchenFood>();
        }

        private static void Hold(KitchenFood food)
        {
            food.GetComponent<KitchenDesktopHeld>().SetHeld(true);
            food.SetDesktopHeld(true);
            Rigidbody body = food.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
        }

        private static void Release(KitchenFood food)
        {
            food.SetDesktopHeld(false);
            RestoreReleasedBody(food);
        }

        private static void RestoreReleasedBody(KitchenFood food)
        {
            food.GetComponent<KitchenDesktopHeld>().SetHeld(false);
            Rigidbody body = food.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
