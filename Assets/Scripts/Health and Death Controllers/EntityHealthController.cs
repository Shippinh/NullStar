using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityHealthController : MonoBehaviour
{
    public int MaxHP = 100;
    [SerializeField] private int CurrentHP = 1;

    [SerializeField] private bool isAlive = true;    // VERY IMPORTANT, this defacto tells if the enemy is dead or alive.

    public bool canBeDamaged = true;                // tells if it can take any damage (in general, it won't take any damage, nor it will react to any if this is false)
    public bool isInvincible = false;               // tells if it can currently take any damage (this controls the on hit invincibility)
    public bool instaKillable = false;              // tells if it can die in one hit (defines if it can be with the soft instakill method InstantlyDie())
    public bool godMode = false;                    // tells if it can die. 

    public bool shouldInvokeGodModeDeath = false;   // tells if fake death in godmode invokes all death related stuff
    public bool shouldAutoReviveGodMode = false;          // tells if an entity should be revived in godmode after dying

    public float invincibilityDuration = 2f;
    public float currentInvincibilityDuration = 0f;

    public bool hasHealed = false;
    public float healingCooldown = 60f;
    public float currentHealingCooldown;

    public event Action BecameInvincible;
    public event Action TookHit;
    public event Action Healed;
    public event Action Died;
    public event Action Revived;

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
        if(isAlive && CurrentHP <= 0 && !godMode)
        {
            // if we detect death in god mode - call death event, revive the entity and don't actually treat it as killed
            Died?.Invoke();

            isAlive = false;

        }

        // Special handle for godmode death
        if (godMode && !isAlive || godMode && CurrentHP <= 0)
        {
            //Fake die conditional
            if(shouldInvokeGodModeDeath)
                Died?.Invoke();

            // Hard revive
            if(shouldAutoReviveGodMode)
                Revive(true);
            
            return;
        }
    }

    public void SetMaxHP(int newHP)
    {
        MaxHP = newHP;
        CurrentHP = newHP;
    }

    /// <summary>
    /// Kills the entity instantly based on internal variables.
    /// </summary>
    public void InstantlyDie()
    {
        if (isAlive && instaKillable && canBeDamaged)
        {
            if(!godMode)
                CurrentHP = 0;
        }
    }

    /// <summary>
    /// Kills the entity instantly and unconditionally. Can't die in god mode. To die in god mode call ForciblyDieOverGodMode().
    /// </summary>
    public void ForciblyDie()
    {
        if (!godMode)
            CurrentHP = 0;
    }

    /// <summary>
    /// Kills the entity instantly and unconditionally. Can die in god mode. To not die in god mode call ForciblyDie().
    /// </summary>
    public void ForciblyDieOverGodMode()
    {
        CurrentHP = 0;
    }

    public void Revive(bool shouldInvoke)
    {
        CurrentHP = MaxHP;
        isAlive = true;

        if (shouldInvoke)
            Revived?.Invoke();
    }

    // Returns the current living status of the enemy
    public bool IsAlive()
    {
        return isAlive;
    }
}
