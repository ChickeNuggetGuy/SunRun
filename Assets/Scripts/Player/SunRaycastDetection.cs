using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using SUPERCharacter;
using UnityEditor.EditorTools;
using UnityEngine;

public class SunRaycastDetection : SerializedMonoBehaviour
{
    #region Variables
    public float rayLength;
    public Light SunLight;
    public Dictionary<DetiectionSpotType, List<SunDetectionSpot>> sunDetectionSpots;
    public PlayerHealth playerHealth;
    [SerializeField] private SUPERCharacterAIO characterAIO;
    #endregion

    #region Functions
    /// <summary>
    /// 
    /// </summary>
    /// <param name="sunDetectionSpot"></param>
    /// <returns>Returns positive when in shade, negative otherwise</returns>
    public bool SunDetection(SunDetectionSpot sunDetectionSpot)
    {
        Vector3 direction = -SunLight.transform.forward;

        if (Physics.Raycast(sunDetectionSpot.transform.position, direction, out RaycastHit hitInfo, rayLength, ~LayerMask.NameToLayer("Player")))
        {
            //Sun Detection hiy an object and is in shade
            Debug.DrawLine(sunDetectionSpot.transform.position, hitInfo.point, Color.green);
            return true;
        }
        else
        {
            Debug.DrawRay(sunDetectionSpot.transform.position, direction, Color.red);
            return false;
        }
    }

    private void Start()
    {
        SunDetectionSpot[] tempArray = FindObjectsByType<SunDetectionSpot>(sortMode: FindObjectsSortMode.InstanceID);
        for (int i = 0; i < tempArray.Length; i++)
        {
            SunDetectionSpot sunDetectionSpot = tempArray[i];

            if (!sunDetectionSpots.ContainsKey(sunDetectionSpot.type))
                sunDetectionSpots.Add(sunDetectionSpot.type, new List<SunDetectionSpot>());

            sunDetectionSpots[sunDetectionSpot.type].Add(sunDetectionSpot);
        }
    }

    void Update()
    {

        foreach (KeyValuePair<DetiectionSpotType, List<SunDetectionSpot>> detectionSpots in sunDetectionSpots)
        {
            float partRate = 0;
            for (int i = 0; i < detectionSpots.Value.Count; i++)
            {
                if (SunDetection(detectionSpots.Value[i]))
                {
                    if (characterAIO.isIdle)
                        partRate += detectionSpots.Value[i].values[MovementState.Idle].rateUoMultiplier * 0.5f;
                    else if (characterAIO.isSprinting)
                        partRate += detectionSpots.Value[i].values[MovementState.Sprinting].rateUoMultiplier * 1.25f;
                    else
                        partRate += detectionSpots.Value[i].values[MovementState.Moving].rateUoMultiplier * 1.85f;
                }
                else
                {
                    if (characterAIO.isIdle)
                        partRate -= detectionSpots.Value[i].values[MovementState.Idle].rateDownMultiplier * 0.5f;
                    else if (characterAIO.isSprinting)
                        partRate -= detectionSpots.Value[i].values[MovementState.Sprinting].rateDownMultiplier * 1.25f;
                    else
                        partRate -= detectionSpots.Value[i].values[MovementState.Moving].rateDownMultiplier * 1.85f;
                }
            }

            playerHealth.TryChangeHealth(partRate * .001f);
        }
    }
    #endregion
}
