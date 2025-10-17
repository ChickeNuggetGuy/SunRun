using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInevntory : SerializedMonoBehaviour
{
	public Dictionary<ItemData, int> items;
	[SerializeField] public InventorySettings inventorySettings;
	[SerializeField] public int currentIndex = 0;


	public List<ItemData> ItemList
	{
		get => items.Keys.ToList();
	}
	#region Functions

	#region Monobehaviour Functions

	private void Awake()
	{
		items = new Dictionary<ItemData, int>();
	}

	private void Start()
	{
		InputManager.Instance.inputActions.UI.ScrollWheel.performed += ScrollWheelOnPerformed;
		InputManager.Instance.inputActions.Player.UseItem.performed += UseItemOnPerformed;
	}



	private void OnDestroy()
	{
		InputManager.Instance.inputActions.UI.ScrollWheel.performed -= ScrollWheelOnPerformed;
		InputManager.Instance.inputActions.Player.UseItem.performed -= UseItemOnPerformed;
	}



	#endregion
	
	#region EventHandlers
	private void UseItemOnPerformed(InputAction.CallbackContext obj)
	{
		if(items == null || items.Count == 0)return;
		if(currentIndex >= items.Count || currentIndex < 0)return;
		
		ItemData currentItem = ItemList[currentIndex];
		
		currentItem.Use(this, true);
		
	}
	
	private void ScrollWheelOnPerformed(InputAction.CallbackContext obj)
	{
		int newIndex = currentIndex;
		if (obj.ReadValue<Vector2>().y > 0)
		{
			Debug.Log("PlayerInevntory Scroll Wheel On performed 1");
			newIndex = currentIndex + 1;
			if (newIndex >= items.Count)
			{
				newIndex = 0;
			}
		}
		else if (obj.ReadValue<Vector2>().y < 0)
		{
			Debug.Log("PlayerInevntory Scroll Wheel On performed -1");
			newIndex -= currentIndex - 1;
			if (newIndex < 0)
			{
				newIndex = items.Count - 1;
			}
		}
		else if (obj.ReadValue<Vector2>().y == 0)
		{
			Debug.Log("PlayerInevntory Scroll Wheel On performed 0");
		}

		currentIndex = newIndex;
	}
	#endregion
	private void AddItem(ItemData item)
	{
		if (items.ContainsKey(item))
		{
			items[item] += 1;
			return;
			;
		}
		else
		{
			items.Add(item, 1);
		}
	}

	public bool TryAddItem(ItemData item)
	{
		if (!CanAddItem(item)) return false;

		AddItem(item);
		return true;
	}

	public bool CanAddItem(ItemData item)
	{
		if (item == null) return false;
		if (!inventorySettings.HasFlag(InventorySettings.CanStackItems))
		{
			if (items.ContainsKey(item)) return false;
		}

		return true;
	}

	private void RemoveItem(ItemData item)
	{
		if (items[item] > 1)
		{
			items[item] -= 1;
		}
		else
		{
			items.Remove(item);
		}
	}

	public bool TryRemoveItem(ItemData item)
	{
		if (!items.ContainsKey(item)) return false;

		RemoveItem(item);
		return true;
	}

	public bool HasItem(ItemData item)
	{
		return items.ContainsKey(item);
	}

	#endregion
}