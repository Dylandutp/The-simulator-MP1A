using System;
using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>Controller movement, aiming, hover feedback, and context interactions.</summary>
    public sealed partial class GymSim
    {
        void UpdateLocomotion(float dt)
        {
            Vector2 axis = move.ReadValue<Vector2>();
            Vector3 forward = Vector3.ProjectOnPlane(headset.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(headset.right, Vector3.up).normalized;
            rigRoot.position += (forward * axis.y + right * axis.x) * 2f * dt;
            Vector2 turnAxis = turn.ReadValue<Vector2>();
            if (Mathf.Abs(turnAxis.x) >= .7f && Time.time >= nextTurnTime)
            {
                rigRoot.Rotate(0f, Mathf.Sign(turnAxis.x) * 45f, 0f);
                nextTurnTime = Time.time + .25f;
            }
        }

        void UpdateHeldPreview()
        {
            if (heldPreview == null) return;
            string selected = Slots[slot];
            heldPreview.name = "Held " + selected;
            heldPreview.GetComponent<Renderer>().material.color = selected == "wrench" ? Color.gray : selected == "rubberMat" ? Color.black : new Color(.25f, .38f, .5f);
        }

        void UpdateRay()
        {
            Ray ray = new Ray(rightHand.position, rightHand.forward);
            GymSimThing next = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 15f)) next = hit.collider.GetComponentInParent<GymSimThing>();
            if (hover != next) { if (hover != null) hover.Highlight(false); hover = next; if (hover != null) hover.Highlight(true); }
            if (hover == null) { tooltip.gameObject.SetActive(false); return; }
            tooltip.gameObject.SetActive(true);
            tooltip.transform.position = hover.transform.position + Vector3.up * 1.1f;
            tooltip.transform.rotation = Quaternion.LookRotation(tooltip.transform.position - headset.position);
            tooltip.text = hover.Tooltip();
        }

        void HandleUse()
        {
            if (use.WasPressedThisFrame()) { if (placing) { Place(); return; } if (hover != null) Interact(hover); else BeginPlacement(Slots[slot]); }
            if (activeMachine != null && (!use.IsPressed() || hover != activeMachine || Vector3.Distance(headset.position, activeMachine.transform.position) > 2.5f)) EndSession();
            if (activeMachine != null && use.IsPressed()) Exercise(Time.deltaTime);
        }

        void Interact(GymSimThing target)
        {
            if (target.kind == "popup") { things.Remove(target); Destroy(target.gameObject); return; }
            if (target.kind == "wallBoard") { OpenBoard(); return; }
            if (target.kind.StartsWith("buy")) { Buy(target.kind.Substring(3)); return; }
            if (target.kind.StartsWith("recipe")) { Craft(int.Parse(target.kind.Substring(6))); return; }
            if (target.kind.StartsWith("wild")) { if (Vector3.Distance(headset.position, target.transform.position) <= 2f) { resources[target.kind.Substring(4)].Change(1); things.Remove(target); Destroy(target.gameObject); Toast("+1 " + target.kind.Substring(4)); } return; }
            if (target.kind == "waterDispenser") { if (target.Get("waterReservoir").value >= 10 && resources["hydration"].value < 100) { target.Get("waterReservoir").Change(-10); resources["hydration"].Change(10); target.sessionParticles.Play(); target.sessionAudio.Play(); questProgress["drink"]++; Toast("+10 hydration"); } return; }
            if (target.kind == "shaker") { OpenShaker(target); return; }
            if (wrenchEquipped && (MachineGain.ContainsKey(target.kind) || target.kind == "rubberMat")) { Repair(target); return; }
            if (MachineGain.ContainsKey(target.kind)) StartSession(target); else BeginPlacement(Slots[slot]);
        }
    }
}
