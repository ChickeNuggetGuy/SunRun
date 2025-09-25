using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerHealth : SerializedMonoBehaviour
{
    #region Varibles
    public static PlayerHealth Instance { get; protected set; }
    public float currentHealth;
    public Vector2Int minMaxValues;
    #endregion
    #region Events
    public event EventHandler<float> onHealthChanged;
    public event EventHandler onPlayerDeath;
    #endregion

    #region Functions
    private void AddHealth(float amountToAdd)
    {
        SetHealth(currentHealth + amountToAdd);
    }

    public bool TryAddHealth(float amountToAdd)
    {
        if (currentHealth + amountToAdd < minMaxValues.y)
        {
            AddHealth(amountToAdd);
            return true;
        }
        else if (currentHealth == minMaxValues.y)
            return false;
        else
        {
            SetHealth(minMaxValues.y);
        }

        return true;
    }

    private void RemoveHealth(float amountToRemove)
    {
        if (currentHealth - amountToRemove > minMaxValues.x)
        {
            SetHealth(currentHealth - amountToRemove);
        }
        else
        {
            PlayerDeath();
        }
    }

    public bool TryRemoveHealth(float amountToRemove, bool killPlayer)
    {
        if (currentHealth - amountToRemove > minMaxValues.x)
        {
            RemoveHealth(amountToRemove);
            return true;
        }
        else
        {
            if (killPlayer)
            {
                RemoveHealth(amountToRemove);
            }
            else
                return false;
        }

        return true;
    }

    public bool TryChangeHealth(float amountToChange)
    {
        if (Mathf.Sign(amountToChange) == -1)
        {
            return TryRemoveHealth(Mathf.Abs(amountToChange), true);
        }
        else if (Mathf.Sign(amountToChange) == 1)
            return TryAddHealth(amountToChange);
        else
            return false;
    }
    private void SetHealth(float newValue)
    {
        currentHealth = newValue;
        onHealthChanged?.Invoke(this, currentHealth);
    }
    public void PlayerDeath()
    {
        currentHealth = minMaxValues.x;
        onPlayerDeath.Invoke(this, EventArgs.Empty);
    }


    #region Monobehavior Functions
    void Awake()
    {
        Instance = this;
        currentHealth = minMaxValues.y;
    }

    #endregion
    #endregion
}
