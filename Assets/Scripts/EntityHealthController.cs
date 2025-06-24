using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityHealthController : MonoBehaviour
{
    public int MaxHP = 100;
    public int CurrentHP = 1;

    public bool isAlive = true;
    public bool isInvincible = false;
    public float invincibilityDuration = 2f;
    float currentInvincibilityDuration;

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

    public void TakeDamage(int takenDamage, bool shouldInvoke)
    {
        if(isInvincible == false && isAlive == true)
        {
            isInvincible = true;

            if (shouldInvoke)
                BecameInvincible?.Invoke();

            if(takenDamage > MaxHP)
            {
                CurrentHP = 0;
                return;
            }

            var newHP = CurrentHP - takenDamage;

            if(newHP <= 0)
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
}
