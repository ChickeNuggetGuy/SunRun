using UnityEngine;

public abstract class Manager<T> : ManagerBase where T : ManagerBase
{
	public static T Instance;
}
