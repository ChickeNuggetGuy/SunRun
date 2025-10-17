using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class ProgrssBar : SerializedMonoBehaviour
{
	[SerializeField] protected Color fillColor;
	[SerializeField] protected Image _progressBar;
	[SerializeField] protected Image _backgroundImage;

	[SerializeField] protected float currentValue;
	[SerializeField] protected (int min, int max) minMaxValues;


	protected virtual void Awake()
	{
		_progressBar.color = fillColor;
	}
	protected void UpdateProgressBar(float newValue)
	{
		currentValue = newValue;
		_progressBar.fillAmount = currentValue / minMaxValues.max;
	}
}
