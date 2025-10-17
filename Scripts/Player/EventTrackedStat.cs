using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class EventTrackedStat : SerializedMonoBehaviour
{
	public StatType stat;
	
	public float CurrentValue {get; protected set;}
	[SerializeField] public (int min, int max) minMaxValues;

	[SerializeField] protected bool eventOnMin;
	[SerializeField] protected bool eventOnMax;
	[SerializeField] protected bool eventOnChnaged;
	
	public event EventHandler StatReachedMin;
	public event EventHandler StatReachedMax;
	
	public event EventHandler StatValueChanged;
	
	void Awake()
	{
		CurrentValue = minMaxValues.max;
	}
	
	private void AddValue(float amountToAdd)
	{
		SetValue(CurrentValue + amountToAdd);
	}

	public bool TryAddValue(float amountToAdd)
	{
		if (CurrentValue + amountToAdd < minMaxValues.max)
		{
			AddValue(amountToAdd);
			return true;
		}
		else if (CurrentValue >= minMaxValues.max)
			return false;
		else
		{
			SetValue(minMaxValues.max);
		}

		return true;
	}

	private void RemoveValue(float amountToRemove)
	{
		if (CurrentValue - amountToRemove > minMaxValues.min)
		{
			SetValue(CurrentValue - amountToRemove);
		}
		else
		{
			MinValueReached();
		}
	}

	public bool TryRemoveValue(float amountToRemove, bool killPlayer)
	{
		if (CurrentValue - amountToRemove > minMaxValues.min)
		{
			RemoveValue(amountToRemove);
			return true;
		}
		else
		{
			if (killPlayer)
			{
				RemoveValue(amountToRemove);
			}
			else
				return false;
		}

		return true;
	}

	public bool TryChangeValue(float amountToChange)
	{
		if (Mathf.Sign(amountToChange) == -1)
		{
			return TryRemoveValue(Mathf.Abs(amountToChange), true);
		}
		else if (Mathf.Sign(amountToChange) == 1)
			return TryAddValue(amountToChange);
		else
			return false;
	}
	private void SetValue(float newValue)
	{
		CurrentValue = newValue;
		if(eventOnChnaged)
			StatValueChanged?.Invoke(this, EventArgs.Empty);
	}
	
	public void MinValueReached()
	{
		CurrentValue = minMaxValues.min;
		if(eventOnMin)
			StatReachedMin?.Invoke(this, EventArgs.Empty);
	}

}
