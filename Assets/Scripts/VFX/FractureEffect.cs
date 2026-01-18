using UnityEngine;

/* 📋 LOGIC MEMO: FractureEffect
--------------------------------------------------
1. Core ($f(x)$):
   - Instantiate N cubes.
   - Velocity = Random.onUnitSphere * force
2. Knobs:
   - pieceCount, explosionForce
3. Usage: Static Spawn() helper.
--------------------------------------------------
*/
public class FractureEffect : MonoBehaviour
{
    public static GameObject Prefab; // Assigned via Inspector on a manager or found by Resource
    
    [SerializeField] private int pieceCount = 8;
    [SerializeField] private float explosionForce = 5f;
    [SerializeField] private GameObject piecePrefab; // Simple small cube

    // Static instance for easy access if needed, or just manual setup
    private static FractureEffect _instance;
    private void Awake() 
    { 
        _instance = this; 
        if(Prefab == null && piecePrefab != null) Prefab = piecePrefab; // Fallback logic
    }

    public static void Spawn(Vector3 position)
    {
        if (_instance == null) return;
        _instance.DoExplode(position);
    }

    private void DoExplode(Vector3 position)
    {
        for (int i = 0; i < pieceCount; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 0.5f;
            GameObject p = Instantiate(piecePrefab, position + offset, Random.rotation);
            p.transform.localScale = Vector3.one * 0.3f;
            
            if (p.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.AddExplosionForce(explosionForce, position, 2f);
            }
            
            Destroy(p, 3f); // Cleanup
        }
    }
}
