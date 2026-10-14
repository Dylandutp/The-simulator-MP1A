using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>Passive resources, machine sessions, repairs, breakdowns, and quests.</summary>
    public sealed partial class GymSim
    {
        void Passive(float dt) { resources["hydration"].Change(-.5f * dt); resources["stress"].Change(.2f * dt); if (activeMachine == null) resources["energy"].Change(.5f * dt); if (resources["energy"].value <= 0) resources["health"].Change(-dt); if (resources["hydration"].value <= 0) resources["health"].Change(-dt); if (resources["health"].value <= 0) Collapse(); }
        void UpdateIdleMachines(float dt) { foreach (var thing in things) { if (!MachineGain.ContainsKey(thing.kind) || thing == activeMachine) continue; thing.Get(thing.kind + "Load").Change(-8f * dt); thing.Get(thing.kind + "Duration").Change(2f * dt); } foreach (var thing in things) if (thing.kind == "waterDispenser") thing.Get("waterReservoir").Change(.5f * dt); }
        void StartSession(GymSimThing machine) { if (machine.Get(machine.kind + "Duration").value <= 0 || machine.Get(machine.kind + "Load").value >= 100 || resources["energy"].value <= 0 || Vector3.Distance(headset.position, machine.transform.position) > 2.5f) return; activeMachine = machine; machine.sessionParticles.Play(); machine.sessionAudio.Play(); Toast("Training " + machine.kind); }
        void Exercise(float dt) { var machine = activeMachine; machine.Get(machine.kind + "Duration").Change(-5f * dt); machine.Get(machine.kind + "Load").Change(10f * dt); machine.Get(machine.kind + "Durability").Change(-dt); resources[MachineGain[machine.kind]].Change(dt); resources["energy"].Change(-4f * dt); resources["stress"].Change(-2f * dt); var mat = MatAt(machine.cell); if (mat != null) mat.Get("matIntegrity").Change(-.2f * dt); if (machine.kind == "treadmill") machine.Get("treadmillMilesRun").Change(.1f * dt); hapticTimer += dt; if (hapticTimer >= 1f) { hapticTimer = 0f; Haptic(.2f, .1f); } if (machine.Get(machine.kind + "Durability").value <= 0) { Break(machine); return; } if (machine.Get(machine.kind + "Duration").value <= 0 || machine.Get(machine.kind + "Load").value >= 100 || resources["energy"].value <= 0) EndSession(); }
        void EndSession() { if (activeMachine != null) { activeMachine.sessionParticles.Stop(); activeMachine.sessionAudio.Stop(); questProgress["sessions"]++; } activeMachine = null; }
        GymSimThing MatAt(Vector2Int cell) { return mats.TryGetValue(cell, out GymSimThing mat) ? mat : null; }
        void Break(GymSimThing machine) { EndSession(); occupancy.Remove(machine.cell); things.Remove(machine); Haptic(1f, .8f); Toast("Broken"); Destroy(machine.gameObject); }
        void Repair(GymSimThing thing) { string stat = thing.kind == "rubberMat" ? "matIntegrity" : thing.kind + "Durability"; if (resources["energy"].value < 5f) return; thing.Get(stat).Change(10f); resources["energy"].Change(-5f); questProgress["repair"]++; Haptic(.15f, .3f); Toast("Repaired"); }
        void Collapse() { EndSession(); resources["energy"].Set(30f); resources["health"].Set(resources["maxHealth"].value); Toast("You collapsed, rest up"); }
        void CheckMilestones() { float low = Mathf.Min(resources["armGain"].value, resources["backGain"].value, resources["chestGain"].value, resources["legGain"].value); while (low >= milestone + 10f) { milestone += 10f; resources["maxHealth"].Change(10f); resources["health"].Change(10f); } questProgress["allGains"] = low >= 10f ? 1 : 0; RewardQuest("sessions", 5, 5); RewardQuest("repair", 3, 5); RewardQuest("craft", 3, 5); RewardQuest("drink", 3, 3); RewardQuest("allGains", 1, 10); }
        void RewardQuest(string name, int threshold, int coins) { if (questProgress.ContainsKey("done" + name) || !questProgress.ContainsKey(name) || questProgress[name] < threshold) return; questProgress["done" + name] = 1; resources["gymCoins"].Change(coins); Toast("Quest complete +" + coins + " coins"); }
    }
}
