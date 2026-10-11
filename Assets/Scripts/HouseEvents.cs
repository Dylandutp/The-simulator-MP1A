using System;
using UnityEngine;

namespace HouseSim
{

/// <summary>
/// Global event bus for the House Sim project. 
/// Routes shared events between rooms and global managers without requiring direct references.
/// </summary>
public static class HouseEvents
{
    /// <summary>
    /// Triggered when a resource is added to the inventory.
    /// </summary>
    public static event Action<ResourceData, int> onResourceHarvest;

    /// <summary>
    /// Triggered when a resource is consumed from the inventory.
    /// </summary>
    public static event Action<ResourceData, int> onResourceSpent;

    /// <summary>
    /// Triggered when a <c>GameObject</c>'s stat changes.
    /// </summary>
    public static event Action<StatData, float, GameObject> onStatChange;

    /// <summary>
    /// Invokes the <c>onResourceHarvest</c> event
    /// </summary>
    /// <param name="resource">The resource data asset being gained</param>
    /// <param name="amount">The quantity gained</param>
    public static void ReportResourceGained(ResourceData resource, int amount)
    {
        onResourceHarvest?.Invoke(resource, amount);
    }

    /// <summary>
    /// Invokes the <c>onResourceSpent</c> event
    /// </summary>
    /// <param name="resource">The resource data asset being spent</param>
    /// <param name="amount">The quantity spent</param>
    public static void ReportResourceSpent(ResourceData resource, int amount)
    {
        onResourceSpent?.Invoke(resource, amount);
    }
}

}   // namespace HouseEvents

