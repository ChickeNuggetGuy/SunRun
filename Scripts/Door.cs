using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class Door : SerializedMonoBehaviour, IInteractable
{
	private bool _isInteractable = true;
	[SerializeField]public bool IsOpen = false;
	[SerializeField] private float _openAngle = -90;
	[SerializeField] private GameObject _pivot;

	bool IInteractable.IsInteractable
	{
		get => _isInteractable;
		set => _isInteractable = value;
	}

	public void Interact(PlayerInteraction playerInteraction)
    {
	    if(!_isInteractable) return;

	    StartCoroutine(Toggle());
    }

	public string InteractText()
	{
		if (IsOpen)
		{
			return "Close";
		}
		else
		{
			return "Open";
		}
	}

	public IEnumerator Toggle()
	{
		print($"Toggling door current { IsOpen}");
		if (IsOpen)yield return StartCoroutine(Close());
		else yield return  StartCoroutine( Open());
	}

	public IEnumerator Open()
	{
		_isInteractable = false;
		yield return _pivot.transform.DOLocalRotate(new Vector3(0, _openAngle, 0), 0.5f, RotateMode.Fast).Play().WaitForCompletion();
		IsOpen = true;
		_isInteractable = true;
	}

	public IEnumerator Close()
	{
		_isInteractable = false;
		yield return _pivot.transform.DOLocalRotate(new Vector3(0, 90, 0), 0.5f, RotateMode.WorldAxisAdd).Play().WaitForCompletion();
		IsOpen = false;
		_isInteractable = true;
	}
	
	
}
