using Sirenix.OdinInspector;
using UnityEngine;

public abstract class ManagerBase : SerializedMonoBehaviour
{
	public abstract string ManagerName { get; }
	[SerializeField] public bool debugMode;
}
