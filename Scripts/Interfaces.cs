using UnityEngine;

interface IInteractable
{
	public bool IsInteractable {get;protected set;}
	public void Interact(PlayerInteraction playerInteraction);
	public string InteractText();
}

