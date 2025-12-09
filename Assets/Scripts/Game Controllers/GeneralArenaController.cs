using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GeneralArenaController : MonoBehaviour
{
    [SerializeField] private List<EnemyController> arenaEnemiesRef; // keeping track of all arena enemies
    public GeneralArenaController nextArenaControllerRef; // reference to the next arena
    public bool hasArenaCompleted = false; // keeping track of current arena state
    public bool hasArenaStarted = false;
    public bool hasNextArenaStarted = false;
    [SerializeField, Range(1, 10)] public int currentWaveCount = 1; // keeping track of waves, do things when each wave starts
    [SerializeField, Range(1, 10)] int totalWaveCount = 5;

    void Start()
    {
        AddEnemiesToList();
    }

    void Update()
    {
        if(hasArenaCompleted == false)
        {
            CheckWave();
        }
    }

    public void AddEnemiesToList()
    {
        EnemyController[] enemies = GetComponentsInChildren<EnemyController>(true);

        foreach (var enemy in enemies)
        {
            if (enemy.countsAsSeparateEnemy)
            {
                arenaEnemiesRef.Add(enemy);
            }
        }
    }

    public void RemoveEnemyFromList(EnemyController enemyToRemove)
    {
        arenaEnemiesRef.Remove(enemyToRemove);
    }

    public void StartWave()
    {
        List<EnemyController> currentWaveEnemiesRef = GrabCurrentWaveEnemies();
        foreach (EnemyController enemyRef in currentWaveEnemiesRef)
        {
            enemyRef.gameObject.SetActive(true);
        }
    }

    private void AdvanceWave()
    {
        if (currentWaveCount < totalWaveCount)
        {
            currentWaveCount++;
            StartWave();
        }
        else
            hasArenaCompleted = true;

        if(hasArenaCompleted == true && hasNextArenaStarted == false && nextArenaControllerRef)
        {
            hasNextArenaStarted = true;
            nextArenaControllerRef.hasArenaCompleted = false;
            gameObject.SetActive(false);
        }
    }

    private void CheckWave()
    {
        if (currentWaveCount == 1 && hasArenaStarted == false)
        {
            hasArenaStarted = true;
            StartWave();
            return;
        }

        // Grab all the current wave enemy references
        List<EnemyController> currentWaveEnemiesRef = GrabCurrentWaveEnemies();

        // Count the amount of dead enemies
        int enemyCount = currentWaveEnemiesRef.Count;
        int deadEnemyCount = 0;
        for (int i = 0; i < enemyCount; i++)
        {
            if (currentWaveEnemiesRef[i].GetHealthController().IsAlive() == false)
                deadEnemyCount++;
        }

        // If dead == total enemies, then advance wave
        if (deadEnemyCount == enemyCount)
        {
            AdvanceWave();
        }
    }

    private List<EnemyController> GrabCurrentWaveEnemies()
    {
        List<EnemyController> currentWaveEnemiesRef = new List<EnemyController>();
        foreach (EnemyController enemyRef in arenaEnemiesRef)
        {
            //Debug.Log(enemyRef);
            if (enemyRef.GetEnemyArenaController().waveToAppear == currentWaveCount)
            {
                currentWaveEnemiesRef.Add(enemyRef);
            }
        }
        return currentWaveEnemiesRef;
    }
}
