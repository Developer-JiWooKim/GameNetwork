using Unity.Netcode;
using UnityEngine;

namespace Assets.MyAssets.Scripts
{
    public class NetworkLauncher : MonoBehaviour
    {
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
    }
}
