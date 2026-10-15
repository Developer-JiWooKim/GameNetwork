using UnityEngine;

namespace Assets.MyAssets.Scripts
{
    public sealed class Billboard : MonoBehaviour
    {
        private Camera _cam;

        void Awake()
        {
            _cam = Camera.main;
        }

        private void LateUpdate()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
            }

            if (_cam == null)
            {
                return;
            }

            transform.rotation = _cam.transform.rotation;
        }
    }
}
