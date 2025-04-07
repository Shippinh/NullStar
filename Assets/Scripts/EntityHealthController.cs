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

    public event Action TookHit;

    void Awake()
    {
        currentInvincibilityDuration = 0f;
    }

    public void TakeDamage(int takenDamage)
    {
        if(isInvincible == false && isAlive == true)
        {
            isInvincible = true;
            TookHit?.Invoke();
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
        }
    }

    // Update is called once per frame
    void Update()
    {
        DetectDeath();
        HandleInvincibility();
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

    private void DetectDeath()
    {
        if(isAlive && CurrentHP <= 0)
        {
            isAlive = false;
        }
    }
}
