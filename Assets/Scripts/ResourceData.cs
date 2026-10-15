using UnityEngine;

namespace HouseSim
{

/// <summary>Shared resource definition used by every room.</summary>
[CreateAssetMenu(fileName = "Resource", menuName = "House Sim/Resource")]
public class ResourceData : ScriptableObject
{
    [Tooltip("Display name, such as Water or Scrap.")]
    public string resourceName;
    public float startingValue;
    public float minValue;
    public float maxValue = 100f;
    public Sprite icon;
    [TextArea] public string description;
    public string DisplayName => string.IsNullOrWhiteSpace(resourceName) ? name : resourceName;
    public Sprite Icon => icon;
    public string Description => description;
    public void Configure(string displayName, string details = "")
    {
        resourceName = displayName;
        description = details;
    }
}    

}   // namespace HouseSim

