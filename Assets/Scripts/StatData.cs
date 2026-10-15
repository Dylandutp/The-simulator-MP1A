using UnityEngine;

namespace HouseSim{

/// <summary>Shared definition for a value tracked by a room object.</summary>
[CreateAssetMenu(fileName = "Stat", menuName = "House Sim/Stat")]
public class StatData : ScriptableObject
{
    public string statName;
    public float defaultValue;
    public float minValue;
    public float maxValue = 100f;
    public Color uiColor = Color.white;
}   

}   // namespace HouseSim

