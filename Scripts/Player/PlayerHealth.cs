using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerHealth : EventTrackedStat
{
    #region Varibles
    public static PlayerHealth Instance { get; protected set; }
    #endregion
    #region Events
    public event EventHandler<float> onHealthChanged;
    #endregion

    #region Functions


    #region Monobehavior Functions


    #endregion
    #endregion
}
