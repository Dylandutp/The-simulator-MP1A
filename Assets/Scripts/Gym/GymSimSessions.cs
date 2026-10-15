using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>Passive resources, machine sessions, repairs, breakdowns, and quests.</summary>
    public sealed partial class GymSim
    {
        void Passive(float dt) { resources["hydration"].Change(-.08f * dt); resources["stress"].Change(.03f * dt); if (activeMachine == null) resources["energy"].Change(.1f * dt); if (resources["energy"].value <= 0) resources["health"].Change(-.15f * dt); if (resources["hydration"].value <= 0) resources["health"].Change(-.15f * dt); if (resources["health"].value <= 0) Collapse(); }
        void UpdateIdleMachines(float dt)
        {
            foreach (GymSimThing thing in things)
            {
                if (thing == null || !MachineGain.ContainsKey(thing.kind) || thing == activeMachine) continue;
                thing.Get(thing.kind + "Load").Change(-1f * dt);
            }
            foreach (GymSimThing thing in things)
            {
                if (thing != null && thing.kind == "waterDispenser")
                    thing.Get("waterReservoir").Change(.2f * dt);
            }
        }
        void CheckFirstMachineApproach()
        {
            if (introPopup == null) introPopup = FindAnyObjectByType<IntroPopup>();
            if (introPopup != null && !introPopup.ReadyForGameplay) return;
            if (machineFirstUsePopup == null) machineFirstUsePopup = FindAnyObjectByType<MachineFirstUsePopup>();
            if (machineFirstUsePopup == null || headset == null) return;

            foreach (GymSimThing thing in things)
            {
                if (thing == null || !thing.placed || !MachineGain.ContainsKey(thing.kind)) continue;
                if (Vector3.Distance(headset.position, thing.transform.position) > 2.5f) continue;
                machineFirstUsePopup.OnMachineApproached(thing.transform);
                return;
            }
        }

        void StartSession(GymSimThing machine)
        {
            if (machine.Get(machine.kind + "Durability").value <= 0f)
            {
                if (machine.kind == "rustyDumbbell") DestroyBrokenRustyDumbbell(machine);
                else Toast("Machine needs repair");
                return;
            }
            if (machine.Get(machine.kind + "Load").value >= 100 || resources["energy"].value <= 0 || Vector3.Distance(headset.position, machine.transform.position) > 2.5f) return;
            activeMachine = machine;
            machine.effects?.PlaySession();
            Toast("Training " + DisplayName(machine.kind) + " - trigger to stop");
        }
        void Exercise(float dt) { var machine = activeMachine; machine.Get(machine.kind + "Load").Change(2.5f * dt); machine.Get(machine.kind + "Durability").Change(-.2f * dt); resources[MachineGain[machine.kind]].Change(.2f * dt); resources["health"].Change(.05f * dt); resources["energy"].Change(-.6f * dt); resources["stress"].Change(-.25f * dt); var mat = MatAt(machine.cell); if (mat != null) mat.Get("matIntegrity").Change(-.03f * dt); if (machine.kind == "treadmill") { machine.Get("treadmillMilesRun").Change(.02f * dt); resources["cardioLevel"].Change(.2f * dt); } hapticTimer += dt; if (hapticTimer >= 1f) { hapticTimer = 0f; Haptic(.2f, .1f); } if (machine.Get(machine.kind + "Durability").value <= 0) { Break(machine); return; } if (machine.Get(machine.kind + "Load").value >= 100 || resources["energy"].value <= 0) EndSession(); }
        void EndSession() { if (activeMachine != null) { activeMachine.effects?.StopSession(); questProgress?.Increment("sessions", 1); } activeMachine = null; }
        GymSimThing MatAt(Vector2Int cell) { return mats.TryGetValue(cell, out GymSimThing mat) ? mat : null; }
        void Break(GymSimThing machine)
        {
            EndSession();
            Haptic(1f, .8f);
            if (machine.kind == "rustyDumbbell") DestroyBrokenRustyDumbbell(machine);
            else Toast("Machine needs repair");
        }
        void DestroyBrokenRustyDumbbell(GymSimThing dumbbell)
        {
            GymEquipmentPlacement placement = dumbbell.GetComponent<GymEquipmentPlacement>();
            if (placement != null) ReleaseFromSocket(placement);
            things.Remove(dumbbell);
            dumbbell.effects?.StopSession();
            Haptic(.35f, .6f);
            Toast("The rusty dumbbell broke");
            Destroy(dumbbell.gameObject);
        }
        void BeginRepair(GymSimThing thing)
        {
            string statName = RepairStat(thing);
            if (thing.Get(statName).value >= thing.Get(statName).max) { Toast("Already fully repaired"); return; }
            if (resources["energy"].value < 5f) { Toast("Need 5 energy to repair"); return; }
            repairTarget = thing;
            repairElapsed = 0f;
            repairStartValue = thing.Get(statName).value;
            resources["energy"].Change(-5f);
            wrenchEffects?.PlaySession();
            Toast("Repairing... trigger to cancel");
        }

        void UpdateRepair(float dt)
        {
            if (repairTarget == null) return;
            if (!wrenchEquipped || wrenchContact == null || !wrenchContact.IsTouching(repairTarget) || headset == null || Vector3.Distance(headset.position, repairTarget.transform.position) > 2.5f) { CancelRepair(); return; }
            repairElapsed += dt;
            Stat repairStat = repairTarget.Get(RepairStat(repairTarget));
            repairStat.Set(Mathf.Lerp(repairStartValue, repairStat.max, Mathf.Clamp01(repairElapsed / 5f)));
            hapticTimer += dt;
            if (hapticTimer >= .8f) { hapticTimer = 0f; Haptic(.1f, .12f); }
            if (repairElapsed < 5f) return;
            questProgress?.Increment("repair", 1);
            wrenchEffects?.StopSession();
            Haptic(.15f, .3f);
            Toast("Repair complete");
            repairTarget = null;
            repairElapsed = 0f;
            repairStartValue = 0f;
        }

        void CancelRepair() { if (repairTarget != null) Toast("Repair cancelled"); wrenchEffects?.StopSession(); repairTarget = null; repairElapsed = 0f; repairStartValue = 0f; }
        bool NeedsRepair(GymSimThing thing)
        {
            if (thing == null || (thing.kind != "rubberMat" && !MachineGain.ContainsKey(thing.kind))) return false;
            Stat repairStat = thing.Get(RepairStat(thing));
            return repairStat.value < repairStat.max;
        }
        static string RepairStat(GymSimThing thing) => thing.kind == "rubberMat" ? "matIntegrity" : thing.kind + "Durability";

        void BeginWaterDispense(GymSimThing dispenser)
        {
            if (activeDispenser != null || dispenser.Get("waterReservoir").value <= 0f || resources["hydration"].value >= 100f) return;
            activeDispenser = dispenser;
            dispenserElapsed = 0f;
            dispensedWater = 0f;
            dispenser.effects?.PlayTimedEffect(dispenser.effects.SessionClip, 2f);
        }

        void UpdateWaterDispense(float dt)
        {
            if (activeDispenser == null) return;
            float amount = Mathf.Min(2.5f * dt, activeDispenser.Get("waterReservoir").value, 100f - resources["hydration"].value);
            activeDispenser.Get("waterReservoir").Change(-amount);
            resources["hydration"].Change(amount);
            dispensedWater += amount;
            dispenserElapsed += dt;
            if (dispenserElapsed < 2f && amount > 0f) return;
            if (dispensedWater > .1f) { questProgress?.Increment("drink", 1); Toast("Hydration +" + dispensedWater.ToString("0")); }
            activeDispenser = null;
        }
        void Collapse() { EndSession(); resources["energy"].Set(30f); resources["health"].Set(resources["maxHealth"].value); Toast("You collapsed, rest up"); }
        void CheckMilestones()
        {
            float lowestGain = Mathf.Min(resources["armGain"].value, resources["backGain"].value, resources["chestGain"].value, resources["legGain"].value);
            questProgress?.ReportValue("armGain", resources["armGain"].value);
            questProgress?.ReportValue("chestGain", resources["chestGain"].value);
            questProgress?.ReportValue("backGain", resources["backGain"].value);
            questProgress?.ReportValue("legGain", resources["legGain"].value);
            questProgress?.ReportValue("allGains", lowestGain);

            float treadmillMiles = 0f;
            foreach (GymSimThing thing in things)
                if (thing != null && thing.kind == "treadmill") treadmillMiles += thing.Get("treadmillMilesRun").value;
            questProgress?.ReportValue("treadmillMiles", treadmillMiles);

            while (lowestGain >= milestone + 10f)
            {
                milestone += 10f;
                resources["maxHealth"].Change(2f);
                resources["health"].max = resources["maxHealth"].value;
            }
        }
    }
}
