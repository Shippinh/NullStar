using System.Collections.Generic;
using UnityEngine;

public class ArenaController : MonoBehaviour
{
    [Header("Arena Settings")]
    public List<ArenaEntitySpawn> spawnPoints = new List<ArenaEntitySpawn>();
    public int totalWaveCount = 5;

    [Header("Arena Flow")]
    public ArenaController nextArenaControllerRef;
    public bool hasArenaCompleted = false;
    public bool hasArenaStarted = false;
    public bool hasNextArenaStarted = false;
    [SerializeField, Range(1, 10)] public int currentWaveCount = 1;

    private List<EnemyController> currentWaveEnemies = new List<EnemyController>();

    void Start()
    {
        StartWaveIfNeeded();
    }

    void Update()
    {
        if (!hasArenaCompleted)
            CheckWaveCompletion();
    }

    private void StartWaveIfNeeded()
    {
        if (currentWaveCount == 1 && !hasArenaStarted)
        {
            hasArenaStarted = true;
            StartWave();
        }
    }

    private void StartWave()
    {
        currentWaveEnemies.Clear();

        foreach (var sp in spawnPoints)
        {
            if (sp.waveToAppear != currentWaveCount) continue;
            GameObject pooledObj = ObjectPool.Instance.GetPooledObject(sp.poolableEnemyTag, sp.transform.position, sp.transform.rotation, false);
            if (pooledObj != null)
            {
                EnemyController enemy = pooledObj.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    currentWaveEnemies.Add(enemy);
                }
            }
        }
    }

    private void CheckWaveCompletion()
    {
        if (currentWaveEnemies.Count == 0) return;

        bool allDead = true;
        foreach (EnemyController enemy in currentWaveEnemies)
        {
            if (enemy.GetHealthController().IsAlive())
            {
                allDead = false;
                break;
            }
        }

        if (allDead)
            AdvanceWave();
    }

    private void AdvanceWave()
    {
        if (currentWaveCount < totalWaveCount)
        {
            currentWaveCount++;
            StartWave();
        }
        else
        {
            hasArenaCompleted = true;

            if (!hasNextArenaStarted && nextArenaControllerRef)
            {
                hasNextArenaStarted = true;
                nextArenaControllerRef.hasArenaCompleted = false;
                gameObject.SetActive(false);
            }
        }
    }
}