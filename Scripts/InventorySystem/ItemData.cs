using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "New ItemData", menuName = "Inventory/ItemDatas")]
public class ItemData : SerializedScriptableObject
{
	[SerializeField] public string ItemName;
	[SerializeField] public string ItemDescription;
	[SerializeField] public int ItemID;
	[SerializeField] public Sprite ItemIcon;
	[SerializeField] public Mesh itemMesh;
	
	[SerializeField] public ItemInteractionType itemInteractionType;
	[SerializeField] public ItemUseSettings itemUseSettings;
	[SerializeField] public List<string> itemActions = new List<string>();


	public void Use(PlayerInevntory playerInevntory, bool calledFromInventory)
	{
		for (int i = 0; i < itemActions.Count; i++)
		{
			Debug.Log(itemActions[i]);
		}

		if (itemUseSettings == ItemUseSettings.Consume)
		{
			if(calledFromInventory)
			playerInevntory.TryRemoveItem(this);
		}

	}
}
