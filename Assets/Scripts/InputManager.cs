using Sirenix.OdinInspector;
using UnityEngine;

public class InputManager : SerializedMonoBehaviour
{
    public static InputManager Instance;
    public InputActions inputActions;

    void Awake()
    {
        Instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        inputActions = new InputActions();
        inputActions.Enable();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
