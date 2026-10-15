using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>Controller movement, aiming, hover feedback, and context interactions.</summary>
    public sealed partial class GymSim
    {
        void UpdateRay()
        {
            // Unity objects compare equal to null after destruction while still holding
            // a managed reference. Clear that reference before calling any component API.
            if (hover == null) hover = null;

            Ray ray = new Ray(rightHand.position, rightHand.forward);
            GymSimThing next = null;
            RaycastHit[] hits = Physics.RaycastAll(ray, 15f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                GymSimThing candidate = hit.collider.GetComponentInParent<GymSimThing>();
                if (candidate == null) continue;
                if (wrenchEquipped && candidate.kind == "wrench") continue;
                next = candidate;
                break;
            }
            if (hover == next) return;

            if (hover != null) hover.GetComponent<HoverOutline>()?.SetManualHighlight(false);
            hover = next;
            if (hover != null) hover.GetComponent<HoverOutline>()?.SetManualHighlight(true);
        }

        void HandleUse()
        {
            if (cancel.WasPressedThisFrame())
            {
                if (repairTarget != null) CancelRepair();
                else if (activeMachine != null) EndSession();
            }

            if (activeMachine == null && TutorialPopupBlocksGameplay()) return;

            if (use.WasPressedThisFrame() && activeMachine == null && repairTarget == null && activeDispenser == null)
            {
                if (wrenchEquipped && wrenchContact != null && wrenchContact.TryGetRepairTarget(out GymSimThing contactTarget) && NeedsRepair(contactTarget))
                {
                    BeginRepair(contactTarget);
                    return;
                }
                if (hover != null) Interact(hover);
            }

            if (activeMachine == null) return;
            if (!activeMachine.placed || Vector3.Distance(headset.position, activeMachine.transform.position) > 2.5f)
            {
                EndSession();
                return;
            }
            Exercise(Time.deltaTime);
        }

        bool TutorialPopupBlocksGameplay()
        {
            if (introPopup == null) introPopup = FindAnyObjectByType<IntroPopup>();
            if (introPopup != null && !introPopup.ReadyForGameplay) return true;
            if (machineFirstUsePopup == null) machineFirstUsePopup = FindAnyObjectByType<MachineFirstUsePopup>();
            return machineFirstUsePopup != null && machineFirstUsePopup.IsOpen;
        }

        void Interact(GymSimThing target)
        {
            if (target.kind == "wallBoard") return;
            if (target.kind.StartsWith("wild"))
            {
                if (Vector3.Distance(headset.position, target.transform.position) <= 2f)
                {
                    resources[target.kind.Substring(4)].Change(1);
                    questProgress?.Increment("wildItems", 1);
                    things.Remove(target);
                    Destroy(target.gameObject);
                    Toast("+1 " + target.kind.Substring(4));
                }
                return;
            }
            if (target.kind == "waterDispenser") { BeginWaterDispense(target); return; }
            if (target.kind == "shaker")
            {
                if (!target.placed) { Toast("Place the shaker before using it"); return; }
                ShakerRecipePopup shakerMenu = target.GetComponent<ShakerRecipePopup>();
                if (shakerMenu == null)
                    shakerMenu = FindAnyObjectByType<ShakerRecipePopup>(FindObjectsInactive.Include);
                if (shakerMenu == null)
                {
                    Toast("Shaker recipe popup is missing");
                    return;
                }
                shakerMenu.OpenShakerMenu(target.effects);
                return;
            }
            if (MachineGain.ContainsKey(target.kind))
            {
                if (!target.placed) { Toast("Place the machine on a rubber mat first"); return; }
                StartSession(target);
            }
        }
    }
}
