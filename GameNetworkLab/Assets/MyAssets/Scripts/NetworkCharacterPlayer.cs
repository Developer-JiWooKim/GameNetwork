using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace Assets.MyAssets.Scripts
{
    [RequireComponent(typeof(CharacterController))]
    public class NetworkCharacterPlayer : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private InputActionReference _moveActionAsset;
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private Renderer _renderer;

        private MaterialPropertyBlock _mpb;
        private CharacterController _cc;
        private InputAction _moveInput;


        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cc.enabled = false;

            _moveInput = _moveActionAsset.action;

        }

        public override void OnNetworkSpawn()
        {
            float hue = (OwnerClientId * 0.618034f) % 1f;
            Color color = Color.HSVToRGB(hue, 0.8f, 0.9f);

            _mpb ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_mpb);

            if (!IsOwner)
            {
                return;
            }

            transform.position = new Vector3(NetworkManager.Singleton.IsHost ? -2f : 2f, 0f, 0f);

            _cc.enabled = true;
            _moveInput.Enable();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            _cc.enabled = false;
            _moveInput.Disable();
        }

        void Update()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            Vector2 input = _moveInput.ReadValue<Vector2>();
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            _cc.Move(dir * _moveSpeed * Time.deltaTime);
        }
    }
}