using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityHealthController : MonoBehaviour
{
    public int MaxHP = 100;
    public int CurrentHP = 1;

    public bool canBeDamaged = true;
    public bool isAlive = true;
    public bool isInvincible = false;
    public bool instaKillable = false;

    public float invincibilityDuration = 2f;
    public float currentInvincibilityDuration = 0f;

    public bool hasHealed = false;
    public float healingCooldown = 60f;
    public float currentHealingCooldown;

    public event Action BecameInvincible;
    public event Action TookHit;
    public event Action Healed;
    public event Action Died;

    void Awake()
    {
        currentInvincibilityDuration = 0f;
        currentHealingCooldown = 0f;
    }

    /// <summary>
    /// Used to deal damage to the entity
    /// </summary>
    /// <param name="takenDamage">The amount of damage</param>
    /// <param name="shouldInvoke">If this damage instance fires TookHit event</param>
    public void TakeDamage(int takenDamage, bool shouldInvoke)
    {
        if (canBeDamaged)
        {
            if (isInvincible == false && isAlive == true)
            {
                isInvincible = true;

                if (shouldInvoke)
                    BecameInvincible?.Invoke();

                if (takenDamage > MaxHP)
                {
                    CurrentHP = 0;
                    return;
                }

                var newHP = CurrentHP - takenDamage;

                if (newHP <= 0)
                {
                    CurrentHP = 0;
                }
                else
                {
                    CurrentHP = newHP;
                }

                if (shouldInvoke)
                    TookHit?.Invoke();
            }
        }
    }

    // Called from separate scripts, ideally in the ai script, the only way to activate HandleHealing()
    public void Heal(int healAmount, bool shouldInvoke)
    {
        if (hasHealed == false && isAlive == true)
        {
            if ((healAmount + CurrentHP) >= MaxHP)
            {
                CurrentHP = MaxHP;
            }
            else
            {
                CurrentHP += healAmount;
            }

            hasHealed = true;
            if (shouldInvoke)
                Healed?.Invoke();
        }
    }

    // Update is called once per frame
    void Update()
    {
        DetectDeath();
        HandleInvincibility();
        HandleHealing();
    }

    private void HandleInvincibility()
    {
        if(isInvincible)
        {
            currentInvincibilityDuration += Time.deltaTime;

            if(currentInvincibilityDuration >= invincibilityDuration)
            {
                isInvincible = false;
                currentInvincibilityDuration = 0f;
            }
        }
    }

    private void HandleHealing()
    {
        if (hasHealed)
        {
            currentHealingCooldown += Time.deltaTime;

            if (currentHealingCooldown >= healingCooldown)
            {
                hasHealed = false;
                currentHealingCooldown = 0f;
            }
        }
    }

    private void DetectDeath()
    {
        if(isAlive && CurrentHP <= 0)
        {
            Died?.Invoke();
            isAlive = false;
        }
    }

    /// <summary>
    /// Kills the current entity instantly based on internal variables
    /// </summary>
    public void InstantlyDie()
    {
        if (isAlive && instaKillable && canBeDamaged)
        {
            CurrentHP = 0;
        }
    }

    /// <summary>
    /// Kills the current entity instantly unconditionally
    /// </summary>
    public void ForciblyDie()
    {
        CurrentHP = 0;
    }
}
