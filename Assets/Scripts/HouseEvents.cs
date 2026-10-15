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
    public static event Action<ResourceData, int> OnResourceGained;
    public static event Action<ResourceData, int> OnResourceSpent;
    public static event Action<StatData, float, GameObject> OnStatChanged;

    /// <summary>
    /// Invokes the <c>onResourceHarvest</c> event
    /// </summary>
    /// <param name="resource">The resource data asset being gained</param>
    /// <param name="amount">The quantity gained</param>
    public static void ReportResourceGained(ResourceData resource, int amount)
    {
        OnResourceGained?.Invoke(resource, amount);
    }

    /// <summary>
    /// Invokes the <c>onResourceSpent</c> event
    /// </summary>
    /// <param name="resource">The resource data asset being spent</param>
    /// <param name="amount">The quantity spent</param>
    public static void ReportResourceSpent(ResourceData resource, int amount)
    {
        OnResourceSpent?.Invoke(resource, amount);
    }

    public static void ReportStatChanged(StatData stat, float value, GameObject source)
    {
        OnStatChanged?.Invoke(stat, value, source);
    }
}

}   // namespace HouseEvents

