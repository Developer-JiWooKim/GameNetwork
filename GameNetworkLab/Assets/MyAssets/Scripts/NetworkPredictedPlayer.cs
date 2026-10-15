using System;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using TMPro;

namespace Assets.MyAssets.Scripts
{
    // 클라이언트 예측 + 서버 보정 + 보정 스무딩 + 렌더링 보간 버전
    // (NetworkTransform 없이 위치를 직접 동기화하므로 프리팹에서 NetworkTransform을 제거해야 함)
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPredictedPlayer : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private const int BufferSize = 1024; // 클라이언트 예측 버퍼 크기 (틱 30 기준 약 34초 분량)
        private const float ReconcileThreshold = 0.01f; // 예측 위치와 서버 위치가 이 값 이상 차이 나면 보정

        // 서버가 확정한 상태: "몇 번 입력까지 처리했고, 그 결과 위치는 여기"
        private struct ServerState : INetworkSerializable, IEquatable<ServerState>
        {
            public int LastProcessedSeq;
            public Vector3 Position;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref LastProcessedSeq);
                serializer.SerializeValue(ref Position);
            }

            public bool Equals(ServerState other)
            {
                return LastProcessedSeq == other.LastProcessedSeq && Position == other.Position;
            }
        }

        [SerializeField] private InputActionReference _moveActionAsset;
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private float _remoteSmoothing = 15f; // 다른 플레이어(원격 캐릭터)를 서버 위치로 따라가게 하는 부드러움 정도
        [SerializeField] private Transform _visualRoot; // 보정 스무딩을 적용할 보이는 부분 (Body, Canvas의 부모)
        [SerializeField] private float _correctionSmoothing = 10f; // 보정 오차를 줄여가는 속도 (클수록 빨리 따라붙음)
        [SerializeField] private float _correctionSnapDistance = 3f; // 오차가 이보다 크면 스무딩 없이 즉시 맞춤

        private MaterialPropertyBlock _mpb;
        private CharacterController _cc;
        private InputAction _moveInput;

        // 서버만 쓰고 모두가 읽는 권위 상태
        private readonly NetworkVariable<ServerState> _serverState = new();

        // 소유자 클라이언트의 예측 기록 (seq % BufferSize 위치에 저장하는 링 버퍼)
        private readonly Vector2[] _inputBuffer = new Vector2[BufferSize];
        private readonly Vector3[] _predictedPositionBuffer = new Vector3[BufferSize];
        private int _currentSeq;
        private float _tickDelta;

        // 보정 스무딩: 루트(실제 위치)는 즉시 보정하고, Visual만 이 오프셋만큼 남겨뒀다가 0으로 줄여간다
        private Vector3 _visualOffset;
        private Vector3 _visualBaseLocalPosition;

        // 렌더링 보간: 틱(30Hz)마다 움직이는 루트 위치를 프레임마다 부드럽게 보여주기 위한 값
        private Vector3 _prevTickPosition;
        private Vector3 _currTickPosition;
        private float _lastTickTime;

        // 서버(호스트) 화면의 클라이언트 캐릭터: RPC 도착 시점마다 움직이는 루트를 Visual이 부드럽게 따라가게 할 위치
        private Vector3 _serverVisualPosition;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cc.enabled = false;

            _moveInput = _moveActionAsset.action;

            if (_visualRoot != null)
            {
                _visualBaseLocalPosition = _visualRoot.localPosition;
            }
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

            // 서버와 클라이언트가 같은 dt로 시뮬레이션해야 예측 결과가 일치한다
            _tickDelta = 1f / NetworkManager.NetworkConfig.TickRate;

            // 이동을 직접 계산하는 쪽(서버, 소유자)만 CharacterController 사용
            _cc.enabled = IsServer || IsOwner;

            if (IsServer)
            {
                // 시작 위치도 서버가 정한다
                Vector3 spawnPos = new Vector3(IsOwnedByServer ? -2f : 2f, 0f, 0f);
                Teleport(spawnPos);
                _serverState.Value = new ServerState { LastProcessedSeq = 0, Position = spawnPos };
            }
            else
            {
                Teleport(_serverState.Value.Position);
            }

            _serverVisualPosition = transform.position;

            if (!IsOwner)
            {
                return;
            }

            if (!IsServer)
            {
                _serverState.OnValueChanged += OnServerStateChanged;
            }

            _prevTickPosition = transform.position;
            _currTickPosition = transform.position;
            _lastTickTime = Time.time;

            NetworkManager.NetworkTickSystem.Tick += OnNetworkTick;
            _moveInput.Enable();
        }

        public override void OnNetworkDespawn()
        {
            _cc.enabled = false;

            if (!IsOwner)
            {
                return;
            }

            _serverState.OnValueChanged -= OnServerStateChanged;

            if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
            {
                NetworkManager.NetworkTickSystem.Tick -= OnNetworkTick;
            }

            _moveInput.Disable();
        }

        // 원격 캐릭터(내 것이 아닌 캐릭터)는 서버 위치를 부드럽게 따라간다
        private void Update()
        {
            if (!IsSpawned || IsOwner || IsServer)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-_remoteSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _serverState.Value.Position, t);
        }

        // 내 캐릭터(소유자: 클라이언트, 호스트)의 Visual 처리
        // 1. 렌더링 보간: 직전 틱 ~ 현재 틱 위치 사이를 프레임마다 보간
        // 2. 보정 스무딩: 보정으로 생긴 오차(_visualOffset)를 서서히 0으로
        private void LateUpdate()
        {
            // 서버(호스트) 화면의 클라이언트 캐릭터는 별도 처리
            if (IsSpawned && IsServer && !IsOwner && _visualRoot != null)
            {
                SmoothServerRemoteVisual();
                return;
            }

            if (!IsSpawned || !IsOwner || _visualRoot == null)
            {
                return;
            }

            if (_visualOffset.magnitude > _correctionSnapDistance)
            {
                _visualOffset = Vector3.zero; // 오차가 너무 크면 끌려오는 게 더 어색하므로 즉시 맞춤
            }

            float t = 1f - Mathf.Exp(-_correctionSmoothing * Time.deltaTime);
            _visualOffset = Vector3.Lerp(_visualOffset, Vector3.zero, t);

            if (_visualOffset.sqrMagnitude < 0.000001f)
            {
                _visualOffset = Vector3.zero;
            }

            // 마지막 틱 이후 흐른 시간 비율 (0 = 방금 틱, 1 = 다음 틱 직전)
            float alpha = Mathf.Clamp01((Time.time - _lastTickTime) / _tickDelta);
            Vector3 renderPosition = Vector3.Lerp(_prevTickPosition, _currTickPosition, alpha) + _visualOffset;

            _visualRoot.localPosition = _visualBaseLocalPosition + transform.InverseTransformVector(renderPosition - transform.position);
        }

        // 서버의 클라이언트 캐릭터: 루트는 RPC가 몰려 도착할 때마다 툭툭 움직이므로 Visual만 따라가게 한다
        // (판정은 루트 기준 그대로, 보이는 것만 부드럽게)
        private void SmoothServerRemoteVisual()
        {
            if (Vector3.Distance(_serverVisualPosition, transform.position) > _correctionSnapDistance)
            {
                _serverVisualPosition = transform.position; // 너무 멀면 즉시 맞춤
            }

            float t = 1f - Mathf.Exp(-_remoteSmoothing * Time.deltaTime);
            _serverVisualPosition = Vector3.Lerp(_serverVisualPosition, transform.position, t);

            _visualRoot.localPosition = _visualBaseLocalPosition + transform.InverseTransformVector(_serverVisualPosition - transform.position);
        }

        // 소유자만 호출됨: 네트워크 틱마다 입력 1개를 처리한다
        private void OnNetworkTick()
        {
            Vector2 input = Vector2.ClampMagnitude(_moveInput.ReadValue<Vector2>(), 1f);

            // 렌더링 보간용: 이번 틱 이동 전 위치를 기억 (이동 후 위치는 아래에서 갱신)
            _prevTickPosition = transform.position;
            _lastTickTime = Time.time;

            // 호스트는 자신이 서버이므로 예측 없이 바로 확정
            if (IsServer)
            {
                Simulate(input);
                _currTickPosition = transform.position;
                _serverState.Value = new ServerState
                {
                    LastProcessedSeq = _serverState.Value.LastProcessedSeq,
                    Position = transform.position
                };
                return;
            }

            // 1. 예측: 서버 응답을 기다리지 않고 로컬에서 먼저 움직인다
            _currentSeq++;
            int index = _currentSeq % BufferSize;

            _inputBuffer[index] = input;
            Simulate(input);
            _predictedPositionBuffer[index] = transform.position;
            _currTickPosition = transform.position;

            // 2. 같은 입력을 시퀀스 번호와 함께 서버로 보낸다
            SubmitInputRPC(_currentSeq, input);
        }

        [Rpc(SendTo.Server)]
        private void SubmitInputRPC(int seq, Vector2 input)
        {
            // 이미 처리한 입력은 무시
            if (seq <= _serverState.Value.LastProcessedSeq)
            {
                return;
            }

            // 클라이언트가 보낸 값을 그대로 믿지 않는다 (입력 크기 제한)
            Simulate(Vector2.ClampMagnitude(input, 1f));

            _serverState.Value = new ServerState
            {
                LastProcessedSeq = seq,
                Position = transform.position
            };
        }

        // 3. 보정: 서버가 확정한 위치와 그 시점의 내 예측 위치를 비교한다
        private void OnServerStateChanged(ServerState previous, ServerState current)
        {
            int serverSeq = current.LastProcessedSeq;

            // 버퍼 범위를 벗어날 만큼 오래된 상태면 서버 위치로 바로 맞춤
            if (_currentSeq - serverSeq >= BufferSize)
            {
                Teleport(current.Position);
                return;
            }

            Vector3 predicted = _predictedPositionBuffer[serverSeq % BufferSize];
            if (Vector3.Distance(predicted, current.Position) < ReconcileThreshold)
            {
                return; // 예측이 맞았음
            }

            Debug.Log($"[Reconcile] seq {serverSeq}: 예측 {predicted} / 서버 {current.Position}, 재적용 입력 {_currentSeq - serverSeq}개");

            // 보정 전 루트 위치 기억 (스무딩용)
            Vector3 rootBeforeReconcile = transform.position;

            // 서버 위치로 되돌린 뒤, 서버가 아직 처리하지 않은 입력을 다시 적용 (replay)
            Teleport(current.Position);
            for (int seq = serverSeq + 1; seq <= _currentSeq; seq++)
            {
                int index = seq % BufferSize;
                Simulate(_inputBuffer[index]);
                _predictedPositionBuffer[index] = transform.position;
            }

            // 루트가 순간이동한 만큼 Visual은 반대로 밀어둬서 화면상 위치를 유지 (LateUpdate에서 서서히 0으로)
            _visualOffset += rootBeforeReconcile - transform.position;

            // 렌더링 보간 구간도 보정된 위치 기준으로 같이 옮김 (화면상 위치는 _visualOffset이 유지)
            Vector3 reconcileDelta = transform.position - rootBeforeReconcile;
            _prevTickPosition += reconcileDelta;
            _currTickPosition += reconcileDelta;
        }

        // 서버와 클라이언트가 공유하는 이동 로직 (같은 입력 + 같은 dt면 같은 결과)
        private void Simulate(Vector2 input)
        {
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            _cc.Move(dir * _moveSpeed * _tickDelta);
        }

        // CharacterController가 켜져 있으면 transform.position 직접 대입이 덮어써질 수 있음
        private void Teleport(Vector3 position)
        {
            bool wasEnabled = _cc.enabled;
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = wasEnabled;
        }

        // Scene 뷰에서 서버 확정 위치를 빨간 박스로 표시 (예측 위치와 비교용)
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !IsSpawned)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_serverState.Value.Position + Vector3.up, new Vector3(1f, 2f, 1f));
        }
    }
}
