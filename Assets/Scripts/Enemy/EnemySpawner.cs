using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/* 📋 LOGIC MEMO: EnemySpawner
--------------------------------------------------
1. Core ($f(x)$): 
   - Position: Center + r * (cos(theta), 0, sin(theta))
   - r ~ Uniform(minRadius, maxRadius)
   - Health ~ Uniform(minHp, maxHp)
2. Knobs: 
   - spawnInterval: Frequency (s)
   - radii: Hollow circle bounds
3. States: Spawning loop on Server only.
--------------------------------------------------
*/
public class EnemySpawner : NetworkBehaviour
{
    [Header("Generation Settings")]
    [Tooltip("Outer radius (a)")]
    [SerializeField] private float maxRadius = 20f;
    [Tooltip("Inner radius (b)")]
    [SerializeField] private float minRadius = 10f;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private GameObject enemyPrefab;

    [Header("Enemy Stats")]
    [SerializeField] private int minHp = 10;
    [SerializeField] private int maxHp = 100;

    private float _timer;

    private void Update()
    {
        if (!IsServer) return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        Vector3 center = GetPlayersCenter();
        
        // Math: Hollow Circle Sampling
        // theta in [0, 2pi]
        float theta = Random.Range(0f, Mathf.PI * 2f);
        // r in [min, max]
        float r = Random.Range(minRadius, maxRadius);
        
        float x = center.x + r * Mathf.Cos(theta);
        float z = center.z + r * Mathf.Sin(theta);
        Vector3 spawnPos = new Vector3(x, 0, z);

        GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = enemyObj.GetComponent<NetworkObject>();
        Enemy enemyScript = enemyObj.GetComponent<Enemy>();

        // Init stats
        int hp = Random.Range(minHp, maxHp);
        enemyScript.InitializeStats(hp, minHp, maxHp);
        
        netObj.Spawn();
    }

    private Vector3 GetPlayersCenter()
    {
        if (NetworkManager.Singleton.ConnectedClientsList.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                sum += client.PlayerObject.transform.position;
                count++;
            }
        }
        return count > 0 ? sum / count : Vector3.zero;
    }
}
