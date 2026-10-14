using System.Collections.Generic;
using UnityEngine;

namespace HouseSim
{

/// <summary>
/// Manages the player's global resource inventory. 
/// Listens to HouseEvents to update quantities dynamically
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    // Maps a ResourceData asset to its current quantity
    private Dictionary<ResourceData, int> inventory = new Dictionary<ResourceData, int>();

    private void OnEnable()
    {
        HouseEvents.OnResourceGained += HandleResourceGained;
        HouseEvents.OnResourceSpent += HandleResourceSpent;
    }

    private void OnDisable()
    {
        HouseEvents.OnResourceGained -= HandleResourceGained;
        HouseEvents.OnResourceSpent -= HandleResourceSpent;
    }

    private void HandleResourceGained(ResourceData resource, int amount)
    {
        if (inventory.ContainsKey(resource))
        {
            inventory[resource] += amount;
        } 
        else
        {
            inventory.Add(resource, amount);
        }
    }

    private void HandleResourceSpent(ResourceData resource, int amount)
    {
        if (inventory.ContainsKey(resource))
        {
            inventory[resource] -= amount;
        } 
        else
        {
            Debug.Log("Error: Player spent a resource that does not exist (not in inventory).");
        }
    }

    /// <summary>
    /// Checks if the inventory contains the required amount of a specific resource.
    /// </summary>
    /// <param name="resource">The resource to check</param>
    /// <param name="requiredAmount">The target quantity required</param>
    /// <returns><c>true</c> if the inventory amount meets or exceeds the required amount</returns>
    public bool HasEnoughResource(ResourceData resource, int requiredAmount)
    {
        if (inventory.ContainsKey(resource) && inventory[resource] >= requiredAmount)
        {
            return true;
        }
        return false;
    }
}

}   // namespace HouseSim
