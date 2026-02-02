using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BodyInfo;

public abstract class SystemModel
{
    public BodyInfo.systemID name;
    public int maxLevel;
    public int currentLevelWithoutDamage;
    public int currentLevel;
    public float currentHealth;

    public SystemModel(int currentLvl)
    {
        currentLevelWithoutDamage = currentLvl;
        currentLevel = currentLvl;
        SetNameAndMaxLevel();
        InitCommands();
    }

    /// <summary>
    /// Set the name to one of the BodyInfo.systemID enums
    /// Set the max level to whatever int you want
    /// </summary>
    public abstract void SetNameAndMaxLevel();

    /// <summary>
    /// This is where you'd set the ICommands that you have a fields to actual
    /// commands relevant to this system.
    ///
    /// For example, if you had ICommand moveForward,
    /// here you would do moveForward = new MoveForwardCommand(this);
    /// </summary>
    protected abstract void InitCommands();

    public virtual void UpgradeLevel(int amount)
    {
        currentLevelWithoutDamage = Mathf.Clamp(currentLevelWithoutDamage + amount, currentLevelWithoutDamage, maxLevel);
    }

    public virtual void Damage(int amount)
    {
        currentLevel = Mathf.Clamp(currentLevel - amount, 0, currentLevel);
    }

    public virtual void DamageHealth(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, currentHealth);
        if (currentHealth <= 0)
        {
            Damage(currentLevel);
        }
    }

    public virtual void Repair(int amount)
    {
        currentLevel = Mathf.Clamp(currentLevel + amount, currentLevel, currentLevelWithoutDamage);
    }
}
