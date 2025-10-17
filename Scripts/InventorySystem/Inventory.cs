using System.Collections.Generic;
using UnityEngine;

public class Inventory
{
	public Dictionary<ItemData, int>  items = new Dictionary<ItemData, int>();
	private bool maxItemLimit = false;
	private int maxItemCount = 0;
	
	private bool allowItemStacking = false;


	public int ItemCount{
		get
		{
			int count = 0;
			if (items == null) return -1;

			foreach (KeyValuePair<ItemData, int> pair in items)
			{
				if(pair.Value == null)continue;
				count  += pair.Value;
			}
			return count;
		}
	}

	private void AddItem(ItemData item, int amount)
	{
		if (items.ContainsKey(item))
		{
			items[item] += amount;
		}
		else
		{
			items.Add(item, amount);
		}
	}

	// public bool TryAddItem(ItemData item, int amount)
	// {
	// 	
	// }

	// public bool CanAddItem(ItemData item, int amount)
	// {
	// 	if(item == null) return false;
	// 	if(maxItemLimit && ItemCount + amount > maxItemCount) return false;
	// 	
	// }
}
