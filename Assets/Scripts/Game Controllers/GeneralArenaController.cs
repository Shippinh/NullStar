using System.Collections.Generic;
using Unity.VisualScripting;
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
        spawnPoints.AddRange(GetComponentsInChildren<ArenaEntitySpawn>(false));
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
            Debug.Log("Depooled " + pooledObj.name + " enemy");
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
        bool allDead = true;

        if (currentWaveEnemies.Count == 0)
        {
            Debug.Log("Arena '" + gameObject.name + "' wave " + currentWaveCount.ToString() + " completed prematurely, no enemy spawns detected");
            AdvanceWave();
            return;
        }

        foreach (EnemyController enemy in currentWaveEnemies)
        {
            if (enemy.GetHealthController().IsAlive())
            {
                allDead = false;
                break;
            }
        }

        if (allDead)
        {
            Debug.Log("Arena '" + gameObject.name + "' wave " + currentWaveCount.ToString() + " completed naturally");
            AdvanceWave();
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
        {
            hasArenaCompleted = true;

            if (!hasNextArenaStarted && nextArenaControllerRef)
            {
                Debug.Log("Arena '" + gameObject.name + "' completed");
                hasNextArenaStarted = true;
                nextArenaControllerRef.hasArenaCompleted = false;
                gameObject.SetActive(false);
            }
        }
    }
}