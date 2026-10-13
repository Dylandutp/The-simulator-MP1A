# House Sim: Shared Systems & Workflow Documentation

To ensure our individual rooms (Bathroom, Kitchen, Garden, Gym, Bedroom) connect properly and don't cause Git conflicts, we are using a **decoupled architecture**. 

Please read this quick guide before building your room's mechanics.

## 1. Scene & Git Workflow (Important for avoiding merge conflicts)

We are using **Additive Scene Loading**. 
* **`MasterScene.unity`:** Contains the XR Origin, the Player Inventory, UI, and Event Managers. Do not place level geometry here.
* **Room Scenes (e.g., `Bathroom.unity`, `Kitchen.unity`):** Contains your specific room's geometry, lighting, and interactive prefabs.

**How to work on your room:**
1. Open `MasterScene` first.
2. Drag your room scene (e.g., `Bathroom`) from the Project window into the Hierarchy so both are open.
3. **Right-click your room scene in the Hierarchy and select "Set Active Scene"** (the text will turn bold).
4. Build your room. When you save, only your room scene is modified on GitHub. You will never get a merge conflict with another room.

## 2. Shared Data (Scriptable Objects)

Instead of using Enums or hardcoding resource names, we use **ScriptableObjects**. This prevents typos (e.g., "Wood" vs "wood") and lets us easily share resources across rooms.

**How to create a new Resource or Stat:**
1. In the Project window, right-click in a shared folder (e.g., `Assets/SharedData/Resources`).
2. Select **Create > House Sim > Resource** (or Stat).
  3. Name the file (e.g., `Water`, `Scrap`, `Hunger`, `WhateverYouWant`).
4. In your scripts (like a crafting station), add a public field: `public ResourceData requiredResource;` and drag the asset into the Inspector.

## 3. Communication: HouseEvents

Your room should **never** directly reference the Master Player UI or other rooms. Instead, we use a static event bus called `HouseEvents`.

When something happens in your room, you "shout" it to the event bus. The Master Scene listens and updates the inventory automatically.

### Gaining a Resource (Harvesting / Shattering)
When a player harvests something in your room, call:
```csharp
HouseEvents.ReportResourceGained(myResourceData, amount);
```

### Spending a Resource (Crafting / Caretaking / Building)
When a player builds or crafts something, call:
```csharp
HouseEvents.ReportResourceSpent(myResourceData, amount);
```
*Note: You should first check if the player has enough by referencing the `PlayerInventory.HasEnoughResource()` method before allowing the spend.*

### Changing a Stat (Collisions / Neighbor Interactions)
When a prefab's stat changes (e.g., a plant grows, a pipe takes damage), call:
```csharp
HouseEvents.OnStatChanged?.Invoke(myStatData, newValue, gameObject);
```

## 4. Example: A Simple Harvestable Object

Here is how you might write a script for a piece of Scrap that can be picked up:

```csharp
using UnityEngine;
using HouseSim; // Make sure to use our namespace

// OR wrapping your code in a more specific namespace works as well
// namespace HouseSim.Bathroom {... your code ...}

public class HarvestableScrap : MonoBehaviour
{
    public ResourceData scrapResource; // Drag your 'Scrap.asset' here in the inspector
    public int yieldAmount = 1;

    // Assuming this triggers when the VR Hand grabs it
    public void OnGrabbed() 
    {
        // Give the resource to the player
        HouseEvents.ReportResourceGained(scrapResource, yieldAmount);
        
        // Destroy the object in the world
        Destroy(gameObject);
    }
}
```