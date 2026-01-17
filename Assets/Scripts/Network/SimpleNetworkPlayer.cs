using Unity.Netcode;
using UnityEngine;

public class SimpleNetworkPlayer : NetworkBehaviour
{
    public NetworkVariable<Color> netColor = new NetworkVariable<Color>();
    public float speed = 5f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Random color for this player
            netColor.Value = new Color(Random.value, Random.value, Random.value);
        }

        // Apply initial color and listen for changes
        ApplyColor(netColor.Value);
        netColor.OnValueChanged += (oldVal, newVal) => ApplyColor(newVal);
    }

    private void ApplyColor(Color c)
    {
        if (GetComponent<Renderer>() != null)
        {
            GetComponent<Renderer>().material.color = c;
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            Vector3 move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            if (move.sqrMagnitude > 0.001f)
            {
                SubmitPositionRequestServerRpc(move);
            }
        }
    }

    [ServerRpc]
    void SubmitPositionRequestServerRpc(Vector3 movement, ServerRpcParams rpcParams = default)
    {
        transform.position += movement * speed * Time.deltaTime;
    }
}
