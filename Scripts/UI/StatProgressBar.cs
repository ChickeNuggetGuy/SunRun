using System;
using UnityEngine;

public class StatProgressBar : ProgrssBar
{
	[SerializeField] private EventTrackedStat statToTrack;


	protected override void Awake()
	{
		base.Awake();
		if (statToTrack == null) return;

		minMaxValues = (statToTrack.minMaxValues.min, statToTrack.minMaxValues.max);
		currentValue = statToTrack.CurrentValue;
    
		// Add this line
		_progressBar.fillAmount = currentValue / minMaxValues.max;

		statToTrack.StatValueChanged += StatOnValueChanged;
	}
	private void StatOnValueChanged(object sender, EventArgs e)
	{
		UpdateProgressBar(statToTrack.CurrentValue);
	}
}
