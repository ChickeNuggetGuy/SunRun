using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : SerializedMonoBehaviour
{
    [SerializeField] private float interactDistance = 3f; 
    [SerializeField] private Transform origin;
    [SerializeField] private LayerMask interactableLayer; 
    [SerializeField] private GameObject interactionUI;
	private IInteractable hoveringInteractable = null;
    private void Start()
    {
        Debug.Log("PlayerInteraction Awake");
        if (InputManager.Instance != null && InputManager.Instance.inputActions != null)
        {
            InputManager.Instance.inputActions.Player.Interact.performed += InteractOnperformed;
        }
        else
        {
            Debug.LogError("InputManager Instance or inputActions is null. Make sure InputManager is set up correctly.");
        }
    }

    private void OnDisable()
    {
        Debug.Log("PlayerInteraction OnDisable");
        if (InputManager.Instance != null && InputManager.Instance.inputActions != null)
        {
            InputManager.Instance.inputActions.Player.Interact.performed -= InteractOnperformed;
        }
    }

    private void Update()
    {
        // For visualizing the raycast in the editor
        Debug.DrawRay(origin.position, Camera.main.transform.forward * interactDistance, Color.red);
        
        if (Physics.Raycast(origin.position, Camera.main.transform.forward, out RaycastHit hit, interactDistance, interactableLayer))
        {
	        if (hit.collider.gameObject.TryGetComponent<IInteractable>(out IInteractable interactable) || 
	            hit.collider.transform.parent.TryGetComponent<IInteractable>(out interactable)
	           )
	        {
		        hoveringInteractable = interactable;
		        
	        }
        }
        else
        {
	        hoveringInteractable = null;
	        Debug.Log($"No interaction with {origin.name}");
        }
        UpdateInteractUI(hoveringInteractable);
    }

    private void UpdateInteractUI(IInteractable interactable)
    {
	    if (interactable != null)
	    {
		    string interactString = interactable.InteractText();

		    interactionUI.GetComponentInChildren<TextMeshProUGUI>().text = interactString;
		    interactionUI.SetActive(true);
	    }
	    else
	    {
		    interactionUI.SetActive(false);
	    }
    }
    private void InteractOnperformed(InputAction.CallbackContext obj)
    {
	    if (hoveringInteractable == null) return;
	    
	    hoveringInteractable.Interact(this);

    }
}