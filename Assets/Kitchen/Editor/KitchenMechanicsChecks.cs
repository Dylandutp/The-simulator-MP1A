using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenMechanicsChecks
    {
        public static void RunAll() { KitchenFoodChecks.Run(); KitchenPlayChecks.Run(); }

        public static IEnumerator Run(Action<bool, string> check, Action complete)
        {
            yield return KitchenControllerChecks.Run(check);
            yield return KitchenRoomScopeChecks.Run(check);
            yield return KitchenPlayerHeightChecks.Run(check);
            yield return KitchenGridAlignmentChecks.Run(check);
            yield return WashAndCut(check);
            yield return Contacts(check);
            yield return KitchenSnapChecks.Run(check);
            yield return KitchenFridgeCycleChecks.Run(check);
            yield return KnifeAndPan(check);
            yield return KitchenPanMotionChecks.Run(check);
            yield return KitchenDesktopPanChecks.Run(check);
            yield return KitchenAudioChecks.Run(check);
            yield return KitchenBowlVisualChecks.Run(check);
            yield return KitchenUseAudioChecks.Run(check);
            yield return KitchenPanSnapChecks.Run(check);
            yield return KitchenKnifeGripChecks.Run(check);
            yield return KitchenKnifeGeometryChecks.Run(check);
            yield return KitchenPanGripChecks.Run(check);
            yield return KitchenRestaurantModelChecks.Run(check);
            yield return KitchenPanHeatChecks.Run(check);
            complete();
        }

        static KitchenFood Food(string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/" + name + ".prefab");
            var food = Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<KitchenFood>();
            foreach (var recovery in food.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            var body = food.GetComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
            return food;
        }

        static Color ColorOf(KitchenFood food)
        {
            return KitchenRestaurantModelChecks.EffectiveColor(KitchenRestaurantModelChecks.ActiveRenderers(food)[0], 0);
        }

        static IEnumerator WashAndCut(Action<bool, string> check)
        {
            foreach (string kind in new[] { "Apple", "Tomato", "Lettuce" })
            {
                var whole = Food(kind, new Vector3(70, 4, 70));
                yield return null;
                Color dirty = ColorOf(whole);
                whole.Wash(.05f);
                check(whole.IsDirty && Mathf.Abs(whole.WashProgress - .5f) < .001f, "wash/" + kind + "/half-time-partial");
                whole.Wash(.05f);
                check(!whole.IsDirty && whole.WashProgress == 1, "wash/" + kind + "/point-one-second-clean");
                check(ColorOf(whole) == dirty, "wash/" + kind + "/color-does-not-jump");
                yield return new WaitForSeconds(.65f);
                Color cleanColor = KitchenRestaurantModelChecks.CleanColor(whole, KitchenRestaurantModelChecks.ActiveRenderers(whole)[0], 0);
                check(Vector4.Distance(ColorOf(whole), cleanColor) < .01f, "wash/" + kind + "/color-finishes-gradually");
                var inventory = KitchenSession.Instance.inventory;
                int before = inventory.GetAmount(whole.definition.preparedResource);
                whole.Claim();
                check(whole.Chop(), "cut/" + kind + "/first-cut");
                var halves = Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).Single(f => f.IsChopped && !f.IsConsumed && f.definition == whole.definition && Vector3.Distance(f.transform.position, whole.transform.position) < .01f);
                var visual = halves.GetComponent<KitchenCutVisual>();
                foreach (var recovery in halves.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                check(halves.CutStage == 1 && visual && visual.PieceCount == KitchenRestaurantModelChecks.ExpectedPieceCount(halves, 1),
                    "cut/" + kind + "/first-cut-matches-selected-model");
                check(!halves.IsDirty && halves.ServingsRemaining == 1 && inventory.GetAmount(halves.definition.preparedResource) == before + 1,
                    "cut/" + kind + "/one-clean-portion");
                check(halves.Chop() && halves.CutStage == 2
                    && visual.PieceCount == KitchenRestaurantModelChecks.ExpectedPieceCount(halves, 2),
                    "cut/" + kind + "/second-cut-matches-selected-model");
                check(!halves.Chop() && halves.GetComponentsInChildren<Rigidbody>().Length == 1 && halves.GetComponentsInChildren<XRGrabInteractable>().Length == 1,
                    "cut/" + kind + "/pieces-picked-up-together-and-stop-after-second-cut");
                check(inventory.GetAmount(halves.definition.preparedResource) == before + 1, "cut/" + kind + "/second-cut-no-inventory-duplication");
                halves.Discard();
                yield return null;
            }
            foreach (float seconds in new[] { 0f, 10f, 20f })
            {
                var steak = Food("Steak", new Vector3(70, 4, 70));
                steak.ApplyHeat(seconds); steak.MarkUnclean(); steak.ApplyHeat(1f);
                var session = KitchenSession.Instance; session.ChangeEnergy(50 - session.Energy);
                check(steak.IsDirty && steak.TryConsume() && session.Energy == 35, "toss/dirty-steak-energy-at-" + seconds);
                yield return null;
            }
        }

        static IEnumerator Contacts(Action<bool, string> check)
        {
            var held = Food("Apple", new Vector3(72, 4, 70));
            var loose = Food("Tomato", held.transform.position + Vector3.right * .3f);
            var looseBody = loose.GetComponent<Rigidbody>(); looseBody.isKinematic = false;
            held.SetDesktopHeld(true);
            yield return new WaitForFixedUpdate();
            var a = held.GetComponent<Collider>(); var b = loose.GetComponent<Collider>();
            check(Physics.GetIgnoreCollision(a, b), "contact/held-food-cannot-bat-other-food");
            Vector3 before = loose.transform.position;
            for (int i = 0; i < 8; i++)
            {
                held.GetComponent<Rigidbody>().MovePosition(held.transform.position + Vector3.right * .08f);
                yield return new WaitForFixedUpdate();
            }
            check(looseBody.linearVelocity.magnitude < .1f && Vector3.Distance(before, loose.transform.position) < .02f, "contact/moving-held-food-does-not-launch-ingredient");
            held.SetDesktopHeld(false);
            yield return new WaitForSeconds(.15f);
            check(!Physics.GetIgnoreCollision(a, b), "contact/released-food-restores-solid-contact");
            check(looseBody.maxDepenetrationVelocity <= .8f && b.sharedMaterial.bounciness == 0, "contact/overlap-and-bounce-damped");
            held.Discard(); loose.Discard(); yield return null;
        }

        static IEnumerator KnifeAndPan(Action<bool, string> check)
        {
            // Motion-specific tests are kept separate from the existing cooking-rule harness.
            yield return KitchenGestureChecks.Run(check);
        }
    }
}
