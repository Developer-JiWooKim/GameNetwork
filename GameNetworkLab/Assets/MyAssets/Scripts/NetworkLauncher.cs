using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.MyAssets.Scripts
{
    public sealed class NetworkLauncher : MonoBehaviour
    {
        public static NetworkLauncher Instance { get; private set; }

        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private Button _scoreButton;

        public event Action ScoreButtonClicked;

        void Awake()
        {
            Instance = this;
            _scoreButton.onClick.AddListener(ScoreButtonClickedHandler);
        }

        private void ScoreButtonClickedHandler()
        {
            ScoreButtonClicked?.Invoke();
        }

        public void StartHost()
        {
            if (NetworkManager.Singleton.IsListening)
            {
                return;
            }

            NetworkManager.Singleton.StartHost();
        }

        public void StartClient()
        {
            if (NetworkManager.Singleton.IsListening)
            {
                return;
            }

            NetworkManager.Singleton.StartClient();
        }

        public void SetText(string text)
        {
            _scoreText.text = text;
        }
    }
}
