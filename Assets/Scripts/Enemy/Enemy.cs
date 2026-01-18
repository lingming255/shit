using UnityEngine;
using Unity.Netcode;

/* 📋 LOGIC MEMO: Enemy
--------------------------------------------------
1. Core ($f(x)$):
   - Move: Dir = (Target - Me).normalized
   - Color = Lerp(Purple, Red, hpRatio)
   - Speed = BaseSpeed * (1 - hpRatio * 0.5) // Example: Higher HP = Slower? User said "High HP move slow"
   - Damage = BaseDamage * hpRatio
2. Knobs:
   - baseSpeed, baseDamage
3. States: ALIVE -> DEAD
--------------------------------------------------
*/
public class Enemy : NetworkBehaviour, IDamageable
{
    [Header("Base Stats")]
    [SerializeField] private float baseSpeed = 5f;
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private Color lowHpColor = new Color(0.5f, 0f, 0.5f); // Purple
    [SerializeField] private Color highHpColor = Color.red;

    // Networked State
    public NetworkVariable<int> currentHp = new NetworkVariable<int>();
    public NetworkVariable<int> maxHp = new NetworkVariable<int>();
    
    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        currentHp.OnValueChanged += OnHpChanged;
        // Initial apply
        UpdateVisuals(currentHp.Value);
    }

    public void InitializeStats(int hp, int minGlobal, int maxGlobal)
    {
        currentHp.Value = hp;
        maxHp.Value = maxGlobal; 
    }

    private void OnHpChanged(int oldVal, int newVal)
    {
        if (newVal <= 0)
        {
            Die();
        }
        UpdateVisuals(newVal);
    }

    private void UpdateVisuals(int hp)
    {
        if (_renderer == null) return;
        
        float ratio = (float)(hp) / (float)maxHp.Value; // Approximate ratio against max possible boss
        _renderer.material.color = Color.Lerp(lowHpColor, highHpColor, ratio);
    }

    private void Update()
    {
        if (!IsServer) return;

        // Logic: Move to nearest player
        Transform target = GetNearestPlayer();
        if (target != null)
        {
            float ratio = (float)currentHp.Value / maxHp.Value;
            // "High HP move slow". 
            // Speed = Base * (1 / (0.5 + ratio)) ? Or linear: Base * (1.5 - ratio)
            // Let's do: Speed = Base * Lerp(1.5f, 0.5f, ratio) -> Big guys move at 0.5x, Small at 1.5x
            float speedMod = Mathf.Lerp(1.5f, 0.5f, ratio);
            
            float step = baseSpeed * speedMod * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, target.position, step);
        }
    }

    private Transform GetNearestPlayer()
    {
        // Simple O(N) search
        float minDist = float.MaxValue;
        Transform best = null;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                float d = Vector3.SqrMagnitude(client.PlayerObject.transform.position - transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    best = client.PlayerObject.transform;
                }
            }
        }
        return best;
    }

    private void OnCollisionEnter(Collision other)
    {
        Debug.Log($"[DEBUG CHECK] IsServer: {IsServer} | Self: {gameObject.name} | Collided with: {other.gameObject.name}");
        if (!IsServer) return;
        Debug.Log($"[Enemy] Collision Enter with {other.gameObject.name}");

        if (other.gameObject.TryGetComponent<IDamageable>(out var victim))
        {
            // "High HP damage high"
            float ratio = (float)currentHp.Value / maxHp.Value;
            int dmg = Mathf.RoundToInt(baseDamage * Mathf.Lerp(0.5f, 2.0f, ratio));
            Debug.Log($"[Enemy] Dealing {dmg} damage to {other.gameObject.name} (Ratio: {ratio:F2})");
            victim.TakeDamage(dmg);        
        }
    }

    public void TakeDamage(int amount)
    {
        if (!IsServer) return;

        // Guard: Prevent double-death logic or operations on invalid objects
        if (currentHp.Value <= 0 || !NetworkObject.IsSpawned) return;

        currentHp.Value -= amount;
        Debug.Log($"[Enemy] Took {amount} dmg. Current HP: {currentHp.Value}/{maxHp.Value}");
        
        // Death handled in OnHpChanged or here.
        if (currentHp.Value <= 0)
        {
            // Logic handled in OnHpChanged for clients? No, spawning FX needs to happen.
            // Spawning FX should be a NetworkObject or just client side VFX?
            // "Separate effect script".
            // We'll call a ClientRpc to spawn FX locally for better performance/physics, 
            // or spawn a Networked Debris object.
            // "Physical simulation" -> Networked rigidbodies can be heavy.
            // Let's spawn a non-networked local prefab on all clients via ClientRpc.
            SpawnDebrisClientRpc(transform.position);
            
            // Final Safety Check: Ensure object is still valid before despawning
            if (NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }
    
    [ClientRpc]
    private void SpawnDebrisClientRpc(Vector3 pos)
    {
        FractureEffect.Spawn(pos);
    }
    
    private void Die() { /* Logic in TakeDamage */ }
}
