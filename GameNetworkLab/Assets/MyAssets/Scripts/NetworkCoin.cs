using Unity.Netcode;
using UnityEngine;

namespace Assets.MyAssets.Scripts
{
    public sealed class NetworkCoin : NetworkBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer)
            {
                return;
            }

            if (!other.TryGetComponent(out NetworkScorePlayer networkScorePlayer))
            {
                return;
            }

            networkScorePlayer.Score.Value += 1;

            NetworkObject.Despawn();
        }
    }
}