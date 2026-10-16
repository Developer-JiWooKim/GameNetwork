using Unity.Netcode;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using Cysharp.Threading.Tasks.Triggers;

namespace Assets.MyAssets.Scripts
{
    public sealed class NetworkCoin : NetworkBehaviour
    {
        private CancellationTokenSource _cts;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            WaitForPickupAsync(_cts.Token).Forget();
        }

        public override void OnNetworkDespawn()
        {
            CtsCancel();
        }

        private async UniTaskVoid WaitForPickupAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                AsyncTriggerEnterTrigger trigger = this.GetAsyncTriggerEnterTrigger();

                while (true)
                {
                    Collider other = await trigger.OnTriggerEnterAsync(cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (!other.TryGetComponent(out NetworkScorePlayer networkScorePlayer))
                    {
                        continue;
                    }

                    networkScorePlayer.Score.Value += 1;
                    NetworkObject.Despawn();

                    break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        private void CtsCancel()
        {
            CancellationTokenSource cts = _cts;
            if (cts is null)
            {
                return;
            }

            _cts = null;

            cts.Cancel();
            cts.Dispose();
        }
    }
}