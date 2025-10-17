using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class ItemHolder : SerializedMonoBehaviour, IInteractable
{
	[SerializeField, OnValueChanged("UpdateItemModel")] ItemData itemData;
	MeshFilter meshFilter;
	private bool _isInteractable;

	private void OnValidate()
	{
		meshFilter = GetComponent<MeshFilter>();
	}

	public void UpdateItemModel()
	{
		if(itemData == null) 
			meshFilter.mesh = null;
		else
			meshFilter.mesh = itemData.itemMesh;
	}

	bool IInteractable.IsInteractable
	{
		get => _isInteractable;
		set => _isInteractable = value;
	}

	public void Interact(PlayerInteraction playerInteraction)
	{
		PlayerInevntory inventory = playerInteraction.GetComponent<PlayerInevntory>();

		switch (itemData.itemInteractionType)
		{
			case ItemInteractionType.PickUp:
				if (inventory.TryAddItem(itemData))
				{
					Debug.Log($"Added item {itemData.ItemName} to inventory");
					Destroy(this.gameObject);
				}
				break;
			case ItemInteractionType.use:
				itemData.Use(inventory, false);

				if (itemData.itemUseSettings == ItemUseSettings.Consume)
				{
					Destroy(this.gameObject);
				}
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

	}

	public string InteractText()
	{
		switch (itemData.itemInteractionType)
		{
			case ItemInteractionType.PickUp:
				return "Pick Up";
				break;
			case ItemInteractionType.use:
				return "Use";
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}
