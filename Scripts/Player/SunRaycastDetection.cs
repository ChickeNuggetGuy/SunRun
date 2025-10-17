using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using StarterAssets;
using UnityEditor.EditorTools;
using UnityEngine;

public class SunRaycastDetection : Manager<SunRaycastDetection>
{
    #region Variables
    public float rayLength;
    public Light SunLight;
    public Dictionary<DetiectionSpotType, SunDetectionSpotData> sunDetectionSpots;
    public PlayerHealth playerHealth;
    [SerializeField] private ThirdPersonController controller;
    
    [Range(0.5f,2)] public float multiplier;
    #endregion

    #region Properties
    public override string ManagerName { get => "Sun Detection"; }
    #endregion

    #region Functions
    /// <summary>
    /// 
    /// </summary>
    /// <param name="sunDetectionSpot"></param>
    /// <returns>Returns positive when in shade, negative otherwise</returns>
    public bool SunDetection(SunDetectionSpot sunDetectionSpot)
    {
        Vector3 direction = -SunLight.transform.forward.normalized;

        if (Physics.Raycast(sunDetectionSpot.transform.position, direction, out RaycastHit hitInfo, rayLength, ~LayerMask.NameToLayer("Player")))
        {
            //Sun Detection hiy an object and is in shade
            if(debugMode)
            {
	            Debug.DrawLine(sunDetectionSpot.transform.position, hitInfo.point, Color.green);
            }
            return true;
        }
        else
        {
	        if (debugMode)
	        {
		        Debug.DrawRay(sunDetectionSpot.transform.position, direction * rayLength, Color.red);
	        }

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
                sunDetectionSpots.Add(sunDetectionSpot.type, new SunDetectionSpotData(new List<SunDetectionSpot>(), 0.5f, 0.5f));

            sunDetectionSpots[sunDetectionSpot.type].sunDetectionPoints.Add(sunDetectionSpot);
        }
    }

    void Update()
    {

        foreach (KeyValuePair<DetiectionSpotType, SunDetectionSpotData> detectionSpots in sunDetectionSpots)
        {
            float partRate = 0;
            for (int i = 0; i < detectionSpots.Value.sunDetectionPoints.Count; i++)
            {
                if (SunDetection(detectionSpots.Value.sunDetectionPoints[i]))
                {
                    if (controller.movementState == MovementState.Idle)
                        partRate += detectionSpots.Value.increaseRate * 1.5f;
                    else if (controller.movementState == MovementState.Sprinting)
                        partRate += detectionSpots.Value.increaseRate * 0.2f;
                    else if (controller.movementState == MovementState.Moving)
	                    partRate += detectionSpots.Value.increaseRate * 0.8f;
                    else
                        partRate += detectionSpots.Value.increaseRate;
                }
                else
                {
	                if (controller.movementState == MovementState.Idle)
		                partRate -= detectionSpots.Value.decreaseRate * 0.5f;
	                else if (controller.movementState == MovementState.Sprinting)
		                partRate -= detectionSpots.Value.decreaseRate * 1.8f;
	                else if (controller.movementState == MovementState.Moving)
		                partRate -= detectionSpots.Value.decreaseRate * 1.2f;
	                else
		                partRate -= detectionSpots.Value.decreaseRate;
                }
            }

            playerHealth.TryChangeValue((partRate *  multiplier) * Time.deltaTime);
        }
    }
    #endregion


}

[System.Serializable]
public class SunDetectionSpotData
{
	[SerializeField]public List<SunDetectionSpot>  sunDetectionPoints;
	[SerializeField]public float increaseRate;
	[SerializeField]public float decreaseRate;

	public SunDetectionSpotData( List<SunDetectionSpot>  points, float increaseRate, float decreaseRate)
	{
		sunDetectionPoints = points;
		this.increaseRate = increaseRate;
		this.decreaseRate = decreaseRate;
	}
}
