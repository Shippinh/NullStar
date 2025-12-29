using System;
using System.Collections.Generic;
using UnityEngine;

public enum ArenaStartMode
{
    OnStart,
    OnPlayerEnterTrigger
}

public enum ArenaState
{
    Idle,               // Exists but not active yet
    WaitingForPlayer,   // Trigger will start arena
    Starting,           // Initializing first wave
    SpawningWave,       // Actively spawning enemies
    WaitingForWaveClear,// Waiting for all enemies to die
    Completed,          // Arena done
    Disabled            // Passed to next arena
}


public class ArenaController : MonoBehaviour
{
    [Header("Arena Settings")]
    public ArenaStartMode startMode = ArenaStartMode.OnStart;
    public List<ArenaEntitySpawn> spawnPoints = new List<ArenaEntitySpawn>();
    public int totalWaveCount = 5;

    [Header("Spawn Timing")]
    public float baseDelay = 0.25f;
    public float randomExtraDelay = 0.1f;

    [Header("Arena Flow")]
    public ArenaController nextArenaControllerRef;

    [Header("Perfect Timer")]
    public float perfectTime = 80f;
    public float currentTime = 0f;

    public bool completeIfPerfectTime = false;


    [Header("Internals")]
    [SerializeField] private int currentWave = 1;
    [SerializeField] private int aliveEnemies = 0;

    [SerializeField] private bool extraWavePossible = true;
    [SerializeField] private bool extraWaveSpawned = false;

    public ArenaState State = ArenaState.Idle;
    public event Action<ArenaState> OnArenaStateChanged;

    private readonly List<ArenaEntitySpawn> pendingSpawns = new List<ArenaEntitySpawn>();
    private float spawnTimer = 0f;

    private readonly Dictionary<EntityHealthController, Action> deathHandlers
        = new Dictionary<EntityHealthController, Action>();

    private bool initialized = false;


    private void Start()
    {
        Initialize();

        switch (startMode)
        {
            case ArenaStartMode.OnStart:
                SetState(ArenaState.Starting);
                StartWave();
                break;

            case ArenaStartMode.OnPlayerEnterTrigger:
                SetState(ArenaState.WaitingForPlayer);
                break;
        }
    }

    public void Initialize()
    {
        if (initialized) return;

        spawnPoints.AddRange(GetComponentsInChildren<ArenaEntitySpawn>(false));
        initialized = true;
    }

    private void Update()
    {
        if (State == ArenaState.Completed || State == ArenaState.Disabled)
            return;

        HandlePerfectTimer();

        HandleSpawning();
    }

    private void HandlePerfectTimer()
    {
        if (!extraWavePossible)
            return;

        // Only count while the arena is actually active
        if (State != ArenaState.Starting &&
            State != ArenaState.SpawningWave &&
            State != ArenaState.WaitingForWaveClear)
            return;

        currentTime += Time.deltaTime;

        if (currentTime >= perfectTime)
        {
            currentTime = perfectTime;
            extraWavePossible = false;
        }
    }


    private void HandleSpawning()
    {
        if (State != ArenaState.SpawningWave)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
            return;

        SpawnNextEnemy();

        if (pendingSpawns.Count > 0)
        {
            spawnTimer = GetNextSpawnDelay();
        }
        else
        {
            // done spawning — wait for cleanup
            SetState(ArenaState.WaitingForWaveClear);
        }
    }

    public void PlayerEnteredTrigger()
    {
        if (State != ArenaState.WaitingForPlayer)
            return;

        SetState(ArenaState.Starting);
        StartWave();
    }

    private void StartWave()
    {
        aliveEnemies = 0;
        pendingSpawns.Clear();

        foreach (var sp in spawnPoints)
            if (sp.waveToAppear == currentWave)
                pendingSpawns.Add(sp);

        if (pendingSpawns.Count == 0)
        {
            AdvanceWave();
            return;
        }

        SetState(ArenaState.SpawningWave);
        spawnTimer = GetNextSpawnDelay();
    }

    private void SpawnNextEnemy()
    {
        var sp = pendingSpawns[0];

        GameObject pooledObj = ObjectPool.Instance.GetPooledObject(
            sp.poolableEnemyTag,
            sp.transform.position,
            sp.transform.rotation,
            false);

        if (!pooledObj)
            return;

        EnemyController enemy = pooledObj.GetComponent<EnemyController>();
        if (!enemy)
            return;

        var hc = enemy.GetHealthController();
        if (!hc)
            return;

        pendingSpawns.RemoveAt(0);
        aliveEnemies++;

        if (deathHandlers.TryGetValue(hc, out var oldHandler))
            hc.Died -= oldHandler;

        Action handler = () => OnEnemyDied(hc);
        deathHandlers[hc] = handler;
        hc.Died += handler;
    }

    private void OnEnemyDied(EntityHealthController hc)
    {
        aliveEnemies--;

        if (aliveEnemies <= 0 && State == ArenaState.WaitingForWaveClear)
        {
            AdvanceWave();
        }
    }

    private void AdvanceWave()
    {
        if (currentWave < totalWaveCount)
        {
            currentWave++;
            StartWave();
            return;
        }

        // consider extra wave
        if (extraWavePossible && !extraWaveSpawned)
        {
            extraWaveSpawned = true;
            currentWave++;
            StartWave();

            if (completeIfPerfectTime)
                CompleteArena();

            return;
        }

        CompleteArena();
    }

    private void CompleteArena()
    {
        SetState(ArenaState.Completed);

        if (nextArenaControllerRef)
        {
            nextArenaControllerRef.gameObject.SetActive(true);
            gameObject.SetActive(false);
            State = ArenaState.Disabled;
        }
    }

    private float GetNextSpawnDelay()
    {
        return baseDelay + UnityEngine.Random.Range(0f, randomExtraDelay);
    }

    private void SetState(ArenaState newState)
    {
        if (State == newState)
            return;

        State = newState;
        OnArenaStateChanged?.Invoke(State);
    }
}
