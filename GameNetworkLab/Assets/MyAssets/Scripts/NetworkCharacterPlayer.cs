using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using TMPro;

namespace Assets.MyAssets.Scripts
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkCharacterPlayer : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private InputActionReference _moveActionAsset;
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private TextMeshProUGUI _nameText;

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
            // 생성 시 Body Renderer의 Material 색깔을 변경
            float hue = OwnerClientId * 0.618034f % 1f;
            Color color = Color.HSVToRGB(hue, 0.8f, 0.9f);

            _mpb ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_mpb);

            _nameText.text = IsOwnedByServer ? $"Host (ID: {OwnerClientId})" : $"Client (ID: {OwnerClientId})";

            _cc.enabled = true;

            if (!IsOwner)
            {
                return;
            }

            transform.position = new Vector3(NetworkManager.Singleton.IsHost ? -2f : 2f, 0f, 0f);

            _moveInput.Enable();
        }

        public override void OnNetworkDespawn()
        {
            _cc.enabled = false;

            if (!IsOwner)
            {
                return;
            }

            _moveInput.Disable();
        }

        void Update()
        {
            if (!IsOwner || !IsSpawned || _moveInput is null)
            {
                return;
            }

            Vector2 input = _moveInput.ReadValue<Vector2>();
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            // _cc.Move(dir * _moveSpeed * Time.deltaTime);

            MoveRequestRPC(dir * _moveSpeed * Time.deltaTime);
        }

        [Rpc(SendTo.Server)]
        private void MoveRequestRPC(Vector3 moveDelta)
        {
            _cc.Move(moveDelta);
        }
    }
}
