using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Assets.MyAssets.Scripts
{
    public sealed class NetworkScorePlayer : NetworkBehaviour
    {
        public NetworkVariable<int> Score = new(0);

        [SerializeField] private TextMeshProUGUI _scoreHUD;

        public void RequestAddScore()
        {
            if (!IsOwner)
            {
                return;
            }

            RequestAddScoreRPC();
        }

        [Rpc(SendTo.Server)]
        private void RequestAddScoreRPC(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            int oldScore = Score.Value;
            Score.Value += 1;
            Debug.Log($"<{OwnerClientId}> 클라이언트가 서버에 점수 변경을 요청하였습니다. [{oldScore} -> {Score.Value}]");

            // UpdateScoreRPC(Score.Value, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        // [Rpc(SendTo.SpecifiedInParams)]
        // private void UpdateScoreRPC(int score, RpcParams rpcParams)
        // {
        //     string text = "Score: " + score.ToString();
        //     NetworkLauncher.Instance.SetText(text);
        // }

        public override void OnNetworkSpawn()
        {
            Score.OnValueChanged += OnScoreChanged;
            UpdateScoreText(Score.Value);

            if (!IsOwner)
            {
                return;
            }

            NetworkLauncher.Instance.ScoreButtonClicked += RequestAddScore;
        }

        public override void OnNetworkDespawn()
        {
            Score.OnValueChanged -= OnScoreChanged;

            if (!IsOwner)
            {
                return;
            }

            NetworkLauncher.Instance.ScoreButtonClicked -= RequestAddScore;
        }

        private void OnScoreChanged(int previous, int current) => UpdateScoreText(current);
        private void UpdateScoreText(int score)
        {
            string text = "Score: " + score.ToString();
            _scoreHUD.text = text;

            if (IsOwner)
            {
                NetworkLauncher.Instance.SetText(text);
            }
        }
    }
}