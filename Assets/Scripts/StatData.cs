using UnityEngine;

namespace HouseSim{

/// <summary>
/// Serves as a data container for prefab stats.
/// Can be instantiated in the project via the Create Asset menu.
/// </summary>
[CreateAssetMenu(fileName = "StatData", menuName = "Scriptable Objects/StatData")]
public class StatData : ScriptableObject
{
    [SerializeField] private string statName;
    [SerializeField] private float maxValue;
    [SerializeField] private Color uiColor; // For health bars, etc.
}   

}   // namespace HouseSim

