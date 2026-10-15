using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Uses real release events and physics updates on the saved pan and food prefabs.</summary>
    public static class KitchenPanSnapChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            var created = new List<GameObject>();
            try
            {
                GameObject station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/InductionHob.prefab");
                KitchenPan pan = Object.Instantiate(station.GetComponentInChildren<KitchenPan>().gameObject,
                    new Vector3(150f, 4f, 150f), Quaternion.Euler(0f, 29f, 0f)).GetComponent<KitchenPan>();
                created.Add(pan.gameObject);
                DisableRecovery(pan.gameObject);
                Rigidbody panBody = pan.GetComponent<Rigidbody>();
                panBody.useGravity = false;
                panBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                panBody.isKinematic = true;
                KitchenDesktopHeld panHeld = pan.gameObject.AddComponent<KitchenDesktopHeld>();

                KitchenFood steak = Spawn("Steak", pan, new Vector3(.20f, .10f, .02f), created);
                Rigidbody body = steak.GetComponent<Rigidbody>();
                Vector3 releasePoint = body.position;
                yield return new WaitForSeconds(.1f);
                check(pan.PlacementCount == 0 && Vector3.Distance(body.position, releasePoint) < .005f,
                    "pan-snap/nearby-unheld-steak-not-pulled-in");
                Hold(steak);
                body.rotation = pan.cookingSurface.rotation * Quaternion.Euler(12f, 24f, -8f);
                steak.transform.rotation = body.rotation;
                Physics.SyncTransforms();
                check(pan.IsInsideSnapRange(steak), "pan-snap/edge-overlap-within-short-placement-range");
                yield return new WaitForSeconds(.1f);
                check(pan.PlacementCount == 0 && steak.IsHeld, "pan-snap/held-steak-never-snaps");
                steak.SetDesktopHeld(false);
                check(pan.PlacementCount == 0 && body.isKinematic,
                    "pan-snap/release-waits-for-controller-physics-restoration");
                RestoreReleasedBody(steak);
                body.linearVelocity = pan.cookingSurface.right * .04f;
                body.angularVelocity = Vector3.up * .12f;
                yield return new WaitForSeconds(.12f);
                Vector3 placed = pan.cookingSurface.InverseTransformPoint(body.position);
                check(pan.PlacementCount == 1 && placed.x * placed.x + placed.z * placed.z < .0001f
                    && Mathf.Abs(placed.y - pan.supportedLocalHeight) < .005f,
                    "pan-snap/released-edge-steak-centers-in-resting-pan");
                check(Quaternion.Angle(body.rotation, pan.cookingSurface.rotation) < .1f
                    && body.linearVelocity.sqrMagnitude < .000001f && body.angularVelocity.sqrMagnitude < .000001f,
                    "pan-snap/placement-levels-food-and-clears-throw-velocity");
                check(!body.isKinematic && !steak.IsHeld && pan.SupportedSteakCount == 0 && !steak.IsDirty,
                    "pan-snap/resting-pan-placement-retains-free-food-state");

                KitchenFood second = Spawn("Steak", pan, new Vector3(0f, .10f, 0f), created);
                Vector3 secondPoint = second.transform.position;
                Hold(second);
                Release(second);
                yield return new WaitForSeconds(.12f);
                check(pan.PlacementCount == 1 && Vector3.Distance(second.transform.position, secondPoint) < .01f,
                    "pan-snap/occupied-pan-does-not-stack-another-steak");
                second.gameObject.SetActive(false);

                body.position = pan.cookingSurface.TransformPoint(new Vector3(.13f, .09f, 0f));
                steak.transform.position = body.position;
                Vector3 displaced = body.position;
                yield return new WaitForSeconds(.1f);
                check(pan.PlacementCount == 1 && Vector3.Distance(body.position, displaced) < .005f,
                    "pan-snap/placement-is-one-shot-not-a-continuous-magnet");
                steak.gameObject.SetActive(false);

                foreach (Vector3 local in new[] { new Vector3(.55f, .1f, 0f), new Vector3(0f, -.19f, 0f), new Vector3(0f, .4f, 0f) })
                {
                    KitchenFood rejected = Spawn("Steak", pan, local, created);
                    Vector3 position = rejected.transform.position;
                    string reason = local.y < 0f ? "below-bottom" : local.y > .2f ? "too-high" : "outside-rim";
                    check(!pan.IsInsideSnapRange(rejected), "pan-snap/" + reason + "-outside-capture-volume");
                    Hold(rejected);
                    Release(rejected);
                    yield return new WaitForSeconds(.1f);
                    check(pan.PlacementCount == 1 && Vector3.Distance(rejected.transform.position, position) < .005f,
                        "pan-snap/" + reason + "-not-teleported");
                    rejected.gameObject.SetActive(false);
                }

                KitchenFood chicken = Spawn("Chicken", pan, new Vector3(0f, .09f, 0f), created);
                Hold(chicken);
                Release(chicken);
                yield return new WaitForSeconds(.1f);
                check(pan.PlacementCount == 1, "pan-snap/only-steak-uses-pan-placement-assist");
                chicken.gameObject.SetActive(false);

                panHeld.SetHeld(true);
                pan.BeginCarry();
                KitchenFood carried = Spawn("Steak", pan, new Vector3(.19f, .1f, 0f), created);
                Rigidbody carriedBody = carried.GetComponent<Rigidbody>();
                Hold(carried);
                Release(carried);
                yield return new WaitForSeconds(.12f);
                check(pan.PlacementCount == 2 && pan.ContainsSupportedFood(carried),
                    "pan-snap/release-into-held-pan-joins-carry-support");
                for (int i = 0; i < 6; i++)
                {
                    panBody.position += Vector3.right * .02f;
                    pan.transform.position = panBody.position;
                    Physics.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                }
                check(Vector3.Distance(carriedBody.position, pan.cookingSurface.TransformPoint(new Vector3(0f, pan.supportedLocalHeight, 0f))) < .03f
                    && !carried.IsDirty, "pan-snap/placed-food-follows-gentle-one-hand-pan-motion");

                check(pan.TryToss(1.4f) && carried.IsDirty && pan.EscapedSteakCount == 1,
                    "pan-snap/deliberate-spill-still-contaminates-placed-steak");
                carriedBody.position = pan.cookingSurface.TransformPoint(new Vector3(0f, pan.supportedLocalHeight, 0f));
                carried.transform.position = carriedBody.position;
                carriedBody.linearVelocity = Vector3.zero;
                carriedBody.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(1.2f);
                check(pan.SupportedSteakCount == 0 && pan.PlacementCount == 2 && carried.IsDirty,
                    "pan-snap/spilled-steak-not-automatically-recaptured-after-contact-grace");
                Hold(carried);
                Release(carried);
                yield return new WaitForSeconds(.12f);
                check(pan.PlacementCount == 3 && pan.ContainsSupportedFood(carried) && carried.IsDirty,
                    "pan-snap/explicit-return-can-reseat-spilled-steak-without-cleaning-it");
            }
            finally
            {
                foreach (GameObject item in created)
                {
                    if (!item) continue;
                    item.SetActive(false);
                    Object.Destroy(item);
                }
            }
        }

        private static KitchenFood Spawn(string name, KitchenPan pan, Vector3 local, List<GameObject> created)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/" + name + ".prefab");
            GameObject item = Object.Instantiate(prefab, pan.cookingSurface.TransformPoint(local), pan.cookingSurface.rotation);
            created.Add(item);
            DisableRecovery(item);
            Rigidbody body = item.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            item.AddComponent<KitchenDesktopHeld>();
            Physics.SyncTransforms();
            return item.GetComponent<KitchenFood>();
        }

        private static void DisableRecovery(GameObject item)
        {
            foreach (KitchenRespawnTool recovery in item.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
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
