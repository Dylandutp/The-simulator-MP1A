using UnityEditor.Search;
using UnityEngine;

namespace HouseSim
{

/// <summary>
/// Serves as a data container for global resources.
/// Can be instantiated in the project via the Create Asset menu.
/// </summary>
[CreateAssetMenu(fileName = "ResourceData", menuName = "Scriptable Objects/ResourceData")]
public class ResourceData : ScriptableObject
{
    [Tooltip("The display name of the resource, e.g., 'Scrap Metal' or 'Water'")]
    [SerializeField] private string resourceName;

    [Tooltip("The icon to display in the player's VR wrist inventory")]
    [SerializeField] private Sprite icon;
    
    [Tooltip("A brief description of what this resource is used for")]
    [SerializeField] private string description;
}    

}   // namespace HouseSim

