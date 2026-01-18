using Unity.Netcode;
using UnityEngine;
using System.IO;

/* 📋 LOGIC MEMO: SimpleNetworkPlayer
--------------------------------------------------
1. Core ($f(x)$):
   - Move: Pos += Input * Speed * dt
   - Attack: Fan Check (Dist < Range && Angle < Limit) -> Damage
   - Health: 0 -> Dead -> Respawn (Reset Pos/HP from JSON)
2. Knobs:
   - attackRange, attackAngle, damage
3. States: ALIVE -> DEAD
--------------------------------------------------
*/
public class SimpleNetworkPlayer : NetworkBehaviour, IDamageable
{
    [System.Serializable]
    public struct PlayerStats
    {
        public int maxHp;
        public float attackRange;
        public float attackAngle;
        public int attackDamage;
        public float attackCooldown;
    }

    public NetworkVariable<Color> netColor = new NetworkVariable<Color>();
    public NetworkVariable<int> currentHp = new NetworkVariable<int>();
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>();

    [Header("Movement")]
    public float speed = 5f;

    [Header("Combat (Overwritten by JSON if present)")]
    public PlayerStats stats = new PlayerStats 
    { 
        maxHp = 100, 
        attackRange = 5f, 
        attackAngle = 90f, 
        attackDamage = 20, 
        attackCooldown = 0.5f 
    };

    private float _lastAttackTime;
    private LineRenderer _attackVisual;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            netColor.Value = new Color(Random.value, Random.value, Random.value);
            LoadStatsFromJson(); // Server authority on stats
            currentHp.Value = stats.maxHp;
            isDead.Value = false;
        }

        ApplyColor(netColor.Value);
        netColor.OnValueChanged += (o, n) => ApplyColor(n);
        currentHp.OnValueChanged += OnHpChanged;
        isDead.OnValueChanged += OnDeathStateChanged;
        
        SetupVisuals();
    }

    private void SetupVisuals()
    {
        // Simple Fan Visualization
        _attackVisual = gameObject.AddComponent<LineRenderer>();
        _attackVisual.positionCount = 3;
        _attackVisual.useWorldSpace = false;
        _attackVisual.startWidth = 0.1f;
        _attackVisual.endWidth = 0.1f;
        _attackVisual.enabled = false; // Only show when attacking? Or always? User said "visualized". 
        // Let's show it on input.
    }

    private void ApplyColor(Color c)
    {
        if (GetComponent<Renderer>() != null) GetComponent<Renderer>().material.color = c;
    }

    private void LoadStatsFromJson()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "player_stats.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            stats = JsonUtility.FromJson<PlayerStats>(json);
        }
    }

    void Update()
    {
        if (isDead.Value) return;

        if (IsOwner)
        {
            HandleInput();
            VisualizeAttackRange();
        }
    }

    private void VisualizeAttackRange()
    {
        // Draw V shape for fan
        float halfAngle = stats.attackAngle * 0.5f * Mathf.Deg2Rad;
        Vector3 left = new Vector3(Mathf.Sin(-halfAngle), 0, Mathf.Cos(-halfAngle)) * stats.attackRange;
        Vector3 right = new Vector3(Mathf.Sin(halfAngle), 0, Mathf.Cos(halfAngle)) * stats.attackRange;
        
        _attackVisual.SetPosition(0, left);
        _attackVisual.SetPosition(1, Vector3.zero);
        _attackVisual.SetPosition(2, right);
        _attackVisual.enabled = true;
    }

    private void HandleInput()
    {
        // Movement
        float x = 0, z = 0;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed) x -= 1;
            if (kb.dKey.isPressed) x += 1;
            if (kb.wKey.isPressed) z += 1;
            if (kb.sKey.isPressed) z -= 1;
            
            // Attack
            if (kb.spaceKey.wasPressedThisFrame && Time.time >= _lastAttackTime + stats.attackCooldown)
            {
                _lastAttackTime = Time.time;
                RequestAttackServerRpc();
            }
        }

        Vector3 move = new Vector3(x, 0, z);
        if (move.sqrMagnitude > 0.001f)
        {
            SubmitPositionRequestServerRpc(move);
        }
    }

    [ServerRpc]
    void SubmitPositionRequestServerRpc(Vector3 movement)
    {
        if (isDead.Value) return;
        transform.position += movement * speed * Time.deltaTime;
    }

    [ServerRpc]
    void RequestAttackServerRpc()
    {
        if (isDead.Value) return;
        Debug.Log($"[Player] ServerRPC Attack Request received. Owner: {OwnerClientId}");

        // Hit Detection
        // Find all IDamageable in range
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.attackRange);
        Debug.Log($"[Player] Attack Hit Scan found {hits.Length} colliders at pos {transform.position}");
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue; // Don't hit self
            
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            // Angle check: Dot(A, B) = cos(theta)
            // If angle < half_fov, then Dot > cos(half_fov)
            if (Vector3.Dot(transform.forward, dir) >= Mathf.Cos(stats.attackAngle * 0.5f * Mathf.Deg2Rad))
            {
                Debug.Log($"[Player] {hit.name} passed angle check.");
                if (hit.TryGetComponent<IDamageable>(out var target))
                {
                    Debug.Log($"[Player] Dealing {stats.attackDamage} damage to {hit.name}");
                    target.TakeDamage(stats.attackDamage);
                    
                    // Visual Feedback
                    if (hit.TryGetComponent<NetworkObject>(out var netObj))
                    {
                        ShowHitFeedbackClientRpc(netObj.NetworkObjectId);
                    }
                }
            }
        }
    }

    [ClientRpc]
    void ShowHitFeedbackClientRpc(ulong targetId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj))
        {
            // Flash effect or similar. Simple debug for now.
            Debug.Log($"Hit {obj.name}!");
            // If it has a renderer, flash it?
            var r = obj.GetComponent<Renderer>();
            if (r) StartCoroutine(FlashRed(r));
        }
    }

    System.Collections.IEnumerator FlashRed(Renderer r)
    {
        Color old = r.material.color;
        r.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        r.material.color = old;
    }

    // IDamageable Implementation
    public void TakeDamage(int amount)
    {
        if (!IsServer || isDead.Value) return;
        
        currentHp.Value -= amount;
        Debug.Log($"[Player] Took {amount} dmg. Current HP: {currentHp.Value}");
        if (currentHp.Value <= 0)
        {
            isDead.Value = true;
            currentHp.Value = 0;
        }
    }

    private void OnHpChanged(int oldVal, int newVal)
    {
        // Update UI HP bar if we had one
    }

    private void OnDeathStateChanged(bool oldVal, bool isNowDead)
    {
        if (isNowDead)
        {
            if (IsOwner)
            {
                // Show Death Screen
                if (PlayerUIManager.Instance != null)
                {
                    PlayerUIManager.Instance.ShowDeathScreen(true);
                }
            }
            // Disable visuals/colliders?
            GetComponent<Renderer>().enabled = false;
            GetComponent<Collider>().enabled = false;
        }
        else
        {
            // Respawned
            if (IsOwner)
            {
                if (PlayerUIManager.Instance != null)
                {
                    PlayerUIManager.Instance.ShowDeathScreen(false);
                }
            }
            GetComponent<Renderer>().enabled = true;
            GetComponent<Collider>().enabled = true;
        }
    }

    public void RequestRespawn()
    {
        if (IsOwner)
        {
            RequestRespawnServerRpc();
        }
    }

    [ServerRpc]
    void RequestRespawnServerRpc()
    {
        LoadStatsFromJson(); // Reload stats just in case
        currentHp.Value = stats.maxHp;
        isDead.Value = false;
        
        // Reset Position to 0,0,0 or spawn point?
        transform.position = new Vector3(0, 1, 0); 
    }
}
