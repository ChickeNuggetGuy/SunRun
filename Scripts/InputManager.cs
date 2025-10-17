using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class InputManager : SerializedMonoBehaviour
{
    public static InputManager Instance;
    public InputActions inputActions;

    private void OnEnable()
    {
	    Instance = this;
	    inputActions = new InputActions();
	    inputActions.Enable();
    }

    private void OnDisable()
    {
	    Instance = null;
	    inputActions.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
