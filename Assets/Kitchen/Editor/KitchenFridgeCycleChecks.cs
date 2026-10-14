using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenFridgeCycleChecks
    {
        /// <summary>Checks actual door travel and Update-driven restocking on an isolated fridge.</summary>
        public static IEnumerator Run(Action<bool, string> assert)
        {
            var created = new List<GameObject>();
            try
            {
                KitchenSession session = KitchenSession.Instance;
                assert(Application.isPlaying && session, "fridge-cycle/live-session");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/Fridge.prefab");
                Vector3 anchor = session.recoveryAnchor ? session.recoveryAnchor.position : Vector3.zero;
                // Above and outside the room, but within food recovery's 25m home radius.
                // This avoids newborn stocked food teleporting before the next test frame.
                GameObject station = Object.Instantiate(prefab, anchor + new Vector3(9f, 4f, 9f), Quaternion.Euler(0f, 31f, 0f));
                created.Add(station);
                KitchenFridge fridge = station.GetComponent<KitchenFridge>();
                int nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                int fullCount = fridge.slots.Length;
                assert(fullCount > 0 && fridge.CountStock() == fullCount,
                    "fridge-cycle/startup-stocks-all-slots");
                foreach (KitchenRespawnTool recovery in station.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                assert(fridge.door.OpenAmount <= 0.01f, "fridge-cycle/starts-closed");

                KitchenFood firstTaken = TakeOne(fridge, created);
                assert(fridge.CountStock() == fullCount - 1, "fridge-cycle/taking-food-leaves-empty-slot");
                SetDoorAmount(fridge.door, 0.08f);
                nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                assert(fridge.CountStock() == fullCount - 1,
                    "fridge-cycle/initial-small-opening-does-not-refill");
                SetDoorAmount(fridge.door, 0.14f);
                nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                assert(fridge.CountStock() == fullCount - 1,
                    "fridge-cycle/open-event-alone-does-not-refill");
                SetDoorAmount(fridge.door, 0.22f);
                nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                assert(fridge.CountStock() == fullCount && firstTaken && firstTaken.IsHeld,
                    "fridge-cycle/meaningful-opening-refills-without-removing-held-food");

                TakeOne(fridge, created);
                // .02 crosses KitchenDoor's old closed event, but is not fully closed.
                foreach (float amount in new[] { 0.10f, 0.24f, 0.02f, 0.22f })
                {
                    SetDoorAmount(fridge.door, amount);
                    nextFrame = Time.frameCount + 3;
                    while (Time.frameCount < nextFrame) yield return null;
                    assert(fridge.CountStock() == fullCount - 1,
                        "fridge-cycle/jiggle-" + amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "-does-not-refill");
                }

                // Exceeds the previous maximum automatic delivery interval (20s), with
                // no global time-scale changes and no private implementation shortcuts.
                float idleUntil = Time.time + 21f;
                while (Time.time < idleUntil) yield return null;
                assert(fridge.CountStock() == fullCount - 1,
                    "fridge-cycle/open-fridge-does-not-auto-restock-after-21-seconds");

                SetDoorAmount(fridge.door, 0f);
                nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                assert(fridge.CountStock() == fullCount - 1,
                    "fridge-cycle/full-close-alone-does-not-refill");
                SetDoorAmount(fridge.door, 0.03f);
                nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                assert(fridge.CountStock() == fullCount - 1,
                    "fridge-cycle/tiny-reopening-after-full-close-does-not-refill");
                SetDoorAmount(fridge.door, 0.25f);
                nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                assert(fridge.CountStock() == fullCount,
                    "fridge-cycle/full-close-then-reopen-refills");

                int childFoodCount = station.GetComponentsInChildren<KitchenFood>().Length;
                foreach (float amount in new[] { 0.45f, 0.75f, 1f })
                {
                    SetDoorAmount(fridge.door, amount);
                    nextFrame = Time.frameCount + 3;
                    while (Time.frameCount < nextFrame) yield return null;
                }
                assert(fridge.CountStock() == fullCount
                    && station.GetComponentsInChildren<KitchenFood>().Length == childFoodCount,
                    "fridge-cycle/continued-opening-does-not-duplicate-stock");

                TakeOne(fridge, created);
                SetDoorAmount(fridge.door, 0f);
                nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                SetDoorAmount(fridge.door, 0.3f);
                nextFrame = Time.frameCount + 3;
                while (Time.frameCount < nextFrame) yield return null;
                assert(fridge.CountStock() == fullCount,
                    "fridge-cycle/subsequent-complete-cycle-refills-again");
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

        private static KitchenFood TakeOne(KitchenFridge fridge, List<GameObject> created)
        {
            KitchenFood food = null;
            foreach (Transform slot in fridge.slots)
            {
                food = slot.GetComponentInChildren<KitchenFood>();
                if (food) break;
            }
            if (!food) throw new InvalidOperationException("Fridge-cycle test could not take stocked food.");
            created.Add(food.gameObject);
            foreach (KitchenRespawnTool recovery in food.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            food.SetDesktopHeld(true);
            food.GetComponent<KitchenStockItem>().Release();
            Rigidbody body = food.GetComponent<Rigidbody>();
            body.useGravity = false;
            Vector3 away = fridge.transform.position + fridge.transform.right * (2f + created.Count * 0.55f) + Vector3.up;
            body.position = away;
            food.transform.position = away;
            Physics.SyncTransforms();
            return food;
        }

        private static void SetDoorAmount(KitchenDoor door, float amount)
        {
            float targetAngle = Mathf.Lerp(door.closedAngle, door.openAngle, amount);
            door.DragByDegrees(targetAngle - door.Angle);
        }
    }
}
