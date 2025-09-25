using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using static DetecionSpotModifier;

public class SunDetectionSpot : SerializedMonoBehaviour
{
    public DetiectionSpotType type;
    public string Name;

    [ListDrawerSettings]
    public Dictionary<MovementState, DetecionSpotModifier> values = new Dictionary<MovementState, DetecionSpotModifier>()
    {
        {MovementState.Idle,new DetecionSpotModifier(){rateDownMultiplier = 1.5f, rateUoMultiplier = 0.5f} },
        {MovementState.Moving,new DetecionSpotModifier(){rateDownMultiplier = 1.5f, rateUoMultiplier = 0.5f} },
        {MovementState.Sprinting,new DetecionSpotModifier(){rateDownMultiplier = 1.5f, rateUoMultiplier = 0.5f} },
    };
}
[System.Serializable]
public class DetecionSpotModifier
{

    [MinValue(0)]
    public float rateUoMultiplier;
    [MinValue(0)]
    public float rateDownMultiplier;
}
