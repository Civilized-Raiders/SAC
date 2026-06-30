using Fusion;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

/* =========================================================
 * [Modification History]
 * 2026-05-22 : Player 스크립트 작성 (Fusion Network 적용)
 * 2026-05-26 : 마우스 회전 / 무한궤도 사운드 및 회전값 보정
 * 2026-05-31 : [네트워크 회전 수정] 시점 회전 누적값을 Networked로 변경
 * 2026-06-01 : 카메라 피치와 머리 모델 피치 회전율 분리 (상하반전 수정 포함)
 * 2026-06-01 : 동작에 따른 배터리 소모 계산 적용 (이동 0.1/s, 달리기 0.2/s, 들기 0.5/s)
 * 2026-06-05 : 무한궤도 오브젝트 회전 대신 TrackScroll의 머티리얼 스크롤 제어로 방식 변경
 * 2026-06-05 : FixedUpdateNetwork 내부에 네트워크 버튼(Carry) 입력을 통한 UseStart 연동 추가
 * ========================================================= */

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : NetworkBehaviour
{
    [Header("Tank Movement Settings")]
    public float moveSpeed = 5f;
    public float turnSpeed = 100f;
    private CharacterController controller;
    private Vector3 velocity;

    [Header("Body & Camera Settings")]
    public Transform upperBody;
    public Transform head;
    public Transform playerCamera;

    public float lookSensitivity = 2f;
    public float maxUpperBodyYaw = 150f;
    public float maxCameraPitch = 60f;
    public float maxHeadModelPitch = 15f;

    [Networked] private float upperBodyYaw { get; set; }
    [Networked] private float cameraPitch { get; set; }

    [Networked] private NetworkButtons previousButtons { get; set; }
    private Quaternion initialUpperBodyRotation;
    private Quaternion initialHeadRotation;
    private Quaternion initialCameraRotation;

    private float prevYawForAudio;
    private float prevPitchForAudio;
    // 클라이언트의 시각/오디오용 렌더 타임 이전 프레임 값 캐시
    private Vector3 prevRenderPos;
    private float prevRenderYaw;

    [Header("Track / Wheel Settings")]
    public Transform leftTrack;
    public Transform rightTrack;
    public float trackRotationSpeed = 360f;

    private TrackScroll leftTrackScroll;
    private TrackScroll rightTrackScroll;

    [Header("LightOnOff")]
    public bool isLightOn = false;

    [SerializeField]
    private GameObject headLight;

    [Header("Audio Settings")]
    public AudioSource trackAudioSource;
    public AudioClip trackMoveClip;
    public AudioSource headAudioSource;
    public AudioClip headRotateClip;
    public AudioClip disChargedClip;
    public AudioClip railgunChargingClip;
    public AudioClip railgunShutClip;


    private int turn = 0;

    private Battery playerBattery;
    private ArmsManager armsManager; // 💡 [수정] ArmsManager로 관리 교체
    // 원격 이동 오디오를 위한 오디오 스무딩
    private float smoothedMoveSpeed = 0f;

    public bool isAlive=true;
    public bool enableCameraMove=true;

    private bool hasPlayedDisChagedSound;

    [Networked] private NetworkBool HeadLightOn { get; set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerBattery = GetComponent<Battery>();
        armsManager = GetComponentInChildren<ArmsManager>(); // 💡 매니저 캐싱
        

        

        if (upperBody != null) initialUpperBodyRotation = upperBody.localRotation;
        if (head != null) initialHeadRotation = head.localRotation;
        if (playerCamera != null) initialCameraRotation = playerCamera.localRotation;

        if (leftTrack != null) leftTrackScroll = leftTrack.GetComponent<TrackScroll>();
        if (rightTrack != null) rightTrackScroll = rightTrack.GetComponent<TrackScroll>();

        if (trackAudioSource != null && trackMoveClip != null)
        {
            trackAudioSource.clip = trackMoveClip;
            trackAudioSource.loop = true;
            trackAudioSource.playOnAwake = false;
            // 다른 플레이어의 트랙 사운드를 월드에서 들리도록 3D 공간 오디오로 설정
            trackAudioSource.spatialBlend = 1.0f;
            trackAudioSource.minDistance = 1f;
            trackAudioSource.maxDistance = 20f;
        }

        if (headAudioSource != null && headRotateClip != null)
        {
            headAudioSource.clip = headRotateClip;
            headAudioSource.loop = false;
            headAudioSource.playOnAwake = false;
        }
    }

    void Start()
    {
        if (headLight != null) headLight.SetActive(false);
    }

    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        DropCarriedItemOnBatteryEmpty();
    }

    public override void Spawned()
    {
        Camera cam = playerCamera != null ? playerCamera.GetComponent<Camera>() : null;
        if (HasInputAuthority)
        {
            LockCursor();
            if (cam != null) cam.enabled = true;
            if (headLight != null) headLight.SetActive(false);
            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = true;
        }
        else
        {

            if (cam != null) cam.enabled = false;
            
            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
        }

        prevYawForAudio = upperBodyYaw;
        prevPitchForAudio = cameraPitch;
        prevRenderPos = transform.position;
        prevRenderYaw = transform.eulerAngles.y;
        prevPosForAudio = transform.position;
        // 네트워크 알림 캐시 초기화
        prevNotifiedPickedSeq = LastAuthoritativePickedUpSeq;
        prevNotifiedDroppedSeq = LastAuthoritativeDroppedSeq;

        if (HasStateAuthority)
        {
            HeadLightOn = false;
            LastAuthoritativePickedUp = default(NetworkId);
            LastAuthoritativeDropped = default(NetworkId);
            LastAuthoritativePickedUpSeq = 0;
            LastAuthoritativeDroppedSeq = 0;
        }


        // 이 프로세스에 대기중인 서버 알림 적용
        if (HasInputAuthority && Runner != null)
        {
            var localRef = Runner.LocalPlayer;
            if (localRef != PlayerRef.None)
            {

                if (pendingPicks.TryGetValue(localRef, out var pickId) && !pickId.Equals(default(NetworkId)))
                {
                    //Debug.Log($"[PlayerMove] Applying queued pick for local player {localRef} -> {pickId}");
                    var clientCarryArm = GetComponentInChildren<CarryArm>();
                    if (clientCarryArm != null) clientCarryArm.AuthoritativePickedUp(pickId);
                    pendingPicks.Remove(localRef);
                }



                if (pendingDrops.TryGetValue(localRef, out var dropId) && !dropId.Equals(default(NetworkId)))
                {
                    //Debug.Log($"[PlayerMove] Applying queued drop for local player {localRef} -> {dropId}");
                    var clientCarryArm = GetComponentInChildren<CarryArm>();
                    if (clientCarryArm != null) clientCarryArm.AuthoritativeDropped(dropId);
                    pendingDrops.Remove(localRef);
                }

            }
        }
    }

    // 네트워크드 알림: 서버가 기록하여 RPC 실패 시 요청한 클라이언트가 로컬 상태를 적용할 수 있게 함
    [Networked] public NetworkId LastAuthoritativePickedUp { get; set; }
    [Networked] public NetworkId LastAuthoritativeDropped { get; set; }
    [Networked] public int LastAuthoritativePickedUpSeq { get; set; }
    [Networked] public int LastAuthoritativeDroppedSeq { get; set; }

    private int prevNotifiedPickedSeq;
    private int prevNotifiedDroppedSeq;

    // 스크립트 내 대기 중 알림 큐(추가 파일 없음), 키: PlayerRef
    private static readonly System.Collections.Generic.Dictionary<PlayerRef, NetworkId> pendingPicks = new System.Collections.Generic.Dictionary<PlayerRef, NetworkId>();
    private static readonly System.Collections.Generic.Dictionary<PlayerRef, NetworkId> pendingDrops = new System.Collections.Generic.Dictionary<PlayerRef, NetworkId>();

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private Vector3 lastMoveDir;
    private bool lastIsSprint;

    void PlayerYReset()
    {
        Vector3 re= new Vector3(transform.position.x, 1f, transform.position.z);
        

        if (controller != null) controller.enabled = false;
        transform.position = re;
        velocity = Vector3.zero;
        if(controller!=null) controller.enabled = true;
    }
    public override void FixedUpdateNetwork()
    {
        bool hasInput = GetInput(out NetworkInputData input);
        if (!isAlive)  return;
        if (!enableCameraMove) return;

        

        if (hasInput && HasInputAuthority)
        {
            if (input.buttons.WasPressed(previousButtons, InputButton.Carry))
            {
                if (!Runner.IsResimulation)
                {// 💡 [수정] 이제 내 몸에 현재 달린 무기가 뭔지 ArmsManager에게 묻고 쏘게 합니다.
                    if (armsManager != null && armsManager.CurrentArm != null)
                    {
                        armsManager.HandleFireInput(true);
                    }
                }
            }
            
            //버려
            if (input.buttons.WasPressed(previousButtons, InputButton.Detach))
            {
                if (armsManager != null && armsManager.CurrentArm != null)
                    armsManager.DetachCurrentArm();
            }

            // 💡 만약 Interact(E키)도 여기서 쏜다면 아까 만든 TryInteract()를 호출 (유지)
            if (input.buttons.WasPressed(previousButtons, InputButton.Interact))
            {
                // TryInteract(); 
            }
        }

        

        if (Object == null || !Object.IsValid) return; // 무효하면 조기 반환
        
        // 기존 탱크 조향 로직
        if (HasStateAuthority)
        {
            if (transform.position.y < -1f) PlayerYReset();
            if (hasInput)
            {
                if (input.buttons.WasPressed(previousButtons, InputButton.HeadLight))
                {
                    HeadLightOn = !HeadLightOn;
                }
                if (enableCameraMove)
                {
                    if (IsForkLiftArmEquipped())
                    {
                        upperBodyYaw = 0f;
                    }
                    else
                    {
                        upperBodyYaw = Mathf.Clamp(upperBodyYaw + input.lookDelta.x * lookSensitivity, -maxUpperBodyYaw, maxUpperBodyYaw);
                    }

                    cameraPitch = Mathf.Clamp(cameraPitch + input.lookDelta.y * lookSensitivity, -maxCameraPitch, maxCameraPitch);
                }
                

                bool movementBlocked = IsMovementBlockedByArm() || !HasBatteryPower();
                Vector3 moveDirection = movementBlocked ? Vector3.zero : input.moveDirection;
                bool isSprint = !movementBlocked && input.buttons.IsSet(InputButton.Sprint);

                ApplyMovementAndRotation(moveDirection, isSprint, Runner.DeltaTime);

                CalculateBatteryConsumption(moveDirection, isSprint, Runner.DeltaTime);
            }
            else
            {
                CalculateBatteryConsumption(Vector3.zero, false, Runner.DeltaTime);
            }
        }
        if (hasInput)
        {
            previousButtons = input.buttons;
        }
    }

    private bool IsForkLiftArmEquipped()
    {
        return armsManager != null && armsManager.CurrentArm != null && armsManager.CurrentArm.ArmType == ArmType.ForkLift;
    }

    private bool IsMovementBlockedByArm()
    {
        return armsManager != null && armsManager.CurrentArm != null && armsManager.CurrentArm.ArmType == ArmType.Gun && armsManager.CurrentArm.IsUsing;
    }

    private bool HasBatteryPower()
    {
        bool wasAlive = isAlive;
        bool hasBatteryPower = playerBattery == null || playerBattery.CurrentBatteryValue > 0f;
        if (wasAlive && !hasBatteryPower)
        {
            DropCarriedItemOnBatteryEmpty();
        }
        isAlive = hasBatteryPower;
        return hasBatteryPower;

    }

    private void CalculateBatteryConsumption(Vector3 moveDir, bool isSprint, float deltaTime)
    {
        if (playerBattery == null) return;
        float consumeRate = 0f;

        if (moveDir.sqrMagnitude > 0.01f)
            consumeRate += isSprint ? 0.2f : 0.1f;

        if (consumeRate <= 0f) return;
        float beforeBattery = playerBattery.CurrentBatteryValue;

        playerBattery.DecreaseBattery(consumeRate * deltaTime);

        if (beforeBattery > 0f && playerBattery.CurrentBatteryValue <= 0f)
        {
            DropCarriedItemOnBatteryEmpty();
        }
    }
    private void DropCarriedItemOnBatteryEmpty()
    {
        if (armsManager == null) return;

        if (armsManager.CurrentArm is CarryArm carryArm)
        {
            carryArm.DropCarriedItemIfAny();
        }
    }
    private Vector3 prevPosForAudio;
    private bool isSprintForAudio;

    public override void Render()
    {
        if (headLight != null)
        {
            headLight.SetActive(HeadLightOn);
        }

        if (upperBody != null)
            upperBody.localRotation = initialUpperBodyRotation * Quaternion.Euler(0f, 0f, upperBodyYaw);

        float visualHeadPitch = 0f;
        if (head != null)
        {
            float ratio = cameraPitch / maxCameraPitch;
            visualHeadPitch = ratio * maxHeadModelPitch;
            head.localRotation = initialHeadRotation * Quaternion.Euler(0f, visualHeadPitch, 0f);
        }

        if (playerCamera != null)
        {
            float remainingCameraPitch = cameraPitch - visualHeadPitch;
            playerCamera.localRotation = initialCameraRotation * Quaternion.Euler(-remainingCameraPitch, 0f, 0f);
        }

        // 렌더 변위 델타로 트랙 비주얼 스크롤을 구동하여 원격 클라이언트도 이동을 보게 함
        float dt = Time.deltaTime;
        Vector3 moveDelta = Vector3.zero;
        if (dt > 0f)
            moveDelta = (transform.position - prevRenderPos) / dt; // 월드 단위 m/s

        // 로컬 공간에서의 대략적 전진 속도 (원래는 -transform.right를 전진으로 사용)
        float forward = Vector3.Dot(moveDelta, -transform.right);
        // 대략적 회전 속도 (deg/s)
        float yawDelta = 0f;
        if (dt > 0f) yawDelta = Mathf.DeltaAngle(prevRenderYaw, transform.eulerAngles.y) / dt;

        // 설정된 이동/회전 속도로 대략 [-1,1] 범위로 정규화
        float forwardNormalized = moveSpeed > 0f ? Mathf.Clamp(forward / moveSpeed, -1f, 1f) : 0f;
        float turnNormalized = turnSpeed > 0f ? Mathf.Clamp(yawDelta / turnSpeed, -1f, 1f) : 0f;

        float visualSpeedMultiplier = lastIsSprint ? 1f : 0.5f;
        float leftWheelSpeed = (forwardNormalized + turnNormalized) * visualSpeedMultiplier;
        float rightWheelSpeed = (forwardNormalized - turnNormalized) * visualSpeedMultiplier;
        leftTrackScroll?.Scroll(leftWheelSpeed, Time.deltaTime);
        rightTrackScroll?.Scroll(rightWheelSpeed, Time.deltaTime);


        
        // 헤드 회전 사운드는 로컬 입력 권한(자신의 플레이어)에서만 재생해야 함
        if (HasInputAuthority)
        {
            turn++;
            if (headAudioSource != null && headRotateClip != null && turn > 15)
            {
                turn = 0;
                float delta = Mathf.Abs(upperBodyYaw - prevYawForAudio) + Mathf.Abs(cameraPitch - prevPitchForAudio);
                if (delta > 0.8f && !headAudioSource.isPlaying)
                    headAudioSource.Play();
            }

            prevYawForAudio = upperBodyYaw;
            prevPitchForAudio = cameraPitch;
            prevPosForAudio = transform.position;
        }

        // 트랙 오디오는 원격 플레이어와 로컬 모두에서 들려야 함
        if (trackAudioSource != null && trackMoveClip != null)
        {
            // 렌더 델타에서 원시 속도(m/s)를 계산하고 지터 방지를 위해 약간 스무딩
            float rawSpeed = moveDelta.magnitude;
            smoothedMoveSpeed = Mathf.Lerp(smoothedMoveSpeed, rawSpeed, 0.25f);

            bool moving = smoothedMoveSpeed > 0.05f;
            bool sprintGuess = smoothedMoveSpeed > moveSpeed * 0.9f;
            SetAudio(trackAudioSource, moving, sprintGuess);
        }

        // 다음 프레임을 위한 렌더 프레임 캐시 갱신(로컬/원격 모두 적용)
        prevRenderPos = transform.position;
        prevRenderYaw = transform.eulerAngles.y;

        batteryDischarge();

        // 네트워크드 필드를 통해 서버->클라이언트 권한 픽업/드롭 알림 감지
        if (HasInputAuthority)
        {
            if (LastAuthoritativePickedUpSeq != prevNotifiedPickedSeq)
            {
                if (LastAuthoritativePickedUp.IsValid)
                {
                    //Debug.Log($"[PlayerMove] Detected LastAuthoritativePickedUp seq={LastAuthoritativePickedUpSeq} -> {LastAuthoritativePickedUp}");
                    var clientCarryArm = GetComponentInChildren<CarryArm>();
                    if (clientCarryArm != null)
                    {
                        clientCarryArm.AuthoritativePickedUp(LastAuthoritativePickedUp);
                    }
                }

                prevNotifiedPickedSeq = LastAuthoritativePickedUpSeq;
            }

            if (LastAuthoritativeDroppedSeq != prevNotifiedDroppedSeq)
            {
                if (LastAuthoritativeDropped.IsValid)
                {
                    //Debug.Log($"[PlayerMove] Detected LastAuthoritativeDropped seq={LastAuthoritativeDroppedSeq} -> {LastAuthoritativeDropped}");
                    var clientCarryArm = GetComponentInChildren<CarryArm>();
                    if (clientCarryArm != null)
                    {
                        clientCarryArm.AuthoritativeDropped(LastAuthoritativeDropped);
                    }
                }

                prevNotifiedDroppedSeq = LastAuthoritativeDroppedSeq;
            }
        }
    }
    

    private void SetAudio(AudioSource src, bool shouldPlay, bool sprint = false)
    {
        if (shouldPlay)
        {
            if (!src.isPlaying) src.Play();
            src.pitch = sprint ? 1.2f : 1.0f;
        }
        else
        {
            if (src.isPlaying) src.Stop();
        }
    }

    private void batteryDischarge()
    {
        if (!HasInputAuthority) return;
        if (playerBattery == null) return;

        bool isDisChaged = playerBattery.CurrentBatteryValue <= 0f;

        if (isDisChaged)
        {
            if (hasPlayedDisChagedSound) return;

            if (headAudioSource != null && disChargedClip != null)
            {
                headAudioSource.PlayOneShot(disChargedClip);
            }

            hasPlayedDisChagedSound = true;
            return;
        }

        hasPlayedDisChagedSound = false;
    }
    private void ApplyMovementAndRotation(Vector3 moveDir, bool isSprint, float deltaTime)
    {
        transform.Rotate(0f, moveDir.x * turnSpeed * deltaTime, 0f, Space.Self);

        Vector3 move = -transform.right * moveDir.z;

        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += Physics.gravity.y * deltaTime;

        float currentSpeed = isSprint ? moveSpeed : moveSpeed * 0.5f;
        controller.Move((move * currentSpeed + velocity) * deltaTime);

        float speedMultiplier = isSprint ? 1f : 0.5f;

        float leftWheelSpeed = (moveDir.z + moveDir.x) * speedMultiplier;
        float rightWheelSpeed = (moveDir.z - moveDir.x) * speedMultiplier;
        lastMoveDir = moveDir;
        lastIsSprint = isSprint;
    }

    // RPC 진입점: 클라이언트(자신의 플레이어 오브젝트에 입력 권한)가 서버에
    // 네트워크 아이템 픽업을 요청합니다. StateAuthority에서 실행됩니다.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestPickUpFromClient(NetworkId itemId, RpcInfo info = default)
    {
        if (Runner == null || !Runner.TryFindObject(itemId, out NetworkObject obj) || obj == null) return;
        Carryable carryable = obj.GetComponent<Carryable>();
        if (carryable == null || carryable.IsCarried) return;

        Transform carryPoint = this.transform;
        ArmsManager am = GetComponentInChildren<ArmsManager>();
        if (am != null && am.CurrentArm is CarryArm carryArm)
        {
            carryPoint = carryArm.GetCarryPoint();
        }

        // 💡 시니어 팁: 클라이언트와 서버의 위치 동기화 오차(네트워크 레이턴시)를 고려하여 
        // 검증 반경을 충분히 넉넉하게 잡아야 클라이언트 유저가 억울하게 픽업이 씹히는 일이 없습니다.
        float maxPickUpDistance = 15.0f; // 기존 5.0f에서 확장 또는 레이캐스트 검증으로 대체
        float distSqr = (carryPoint.position - obj.transform.position).sqrMagnitude;
        if (distSqr > maxPickUpDistance * maxPickUpDistance)
        {
            //Debug.LogWarning($"[Server] 픽업 거리가 너무 멉니다. 오차 거라: {Mathf.Sqrt(distSqr)}m");
            return;
        }

        // 검증 통과 후 픽업
        if (!carryable.TryPickUp(carryPoint, Object))
        {
            return;
        }

        if (am != null && am.CurrentArm is CarryArm serverCarryArm)
        {
            serverCarryArm.AuthoritativePickedUp(itemId);
        }

        LastAuthoritativePickedUp = itemId;
        LastAuthoritativePickedUpSeq++;
        LastAuthoritativeDropped = default(NetworkId);
    }

    // RPC 진입점: 클라이언트가 아이템 드롭을 요청할 때 호출
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestDropFromClient(NetworkId itemId, RpcInfo info = default)
    {
        //Debug.Log($"SERVER RPC RECEIVED : {itemId}");
        if (Runner == null)
        {
            //Debug.LogWarning("[PlayerMove] Rpc_RequestDropFromClient - Runner null");
            return;
        }

        if (!Runner.TryFindObject(itemId, out NetworkObject obj) || obj == null)
        {
            //Debug.LogWarning($"[PlayerMove] Rpc_RequestDropFromClient - item not found: {itemId}");
            return;
        }

        Carryable carryable = obj.GetComponent<Carryable>();
        if (carryable == null)
        {
            //Debug.LogWarning($"[PlayerMove] Rpc_RequestDropFromClient - target not Carryable: {obj.name}");
            return;
        }

        //Debug.Log($"[PlayerMove] Rpc_RequestDropFromClient - player={this.gameObject.name}, item={obj.name}, itemIsCarried={carryable.IsCarried}, itemPos={obj.transform.position}, playerPos={transform.position}");

        if (!carryable.IsCarried)
        {
            // Debug.Log($"[PlayerMove] Rpc_RequestDropFromClient - item not currently carried: {obj.name}");
            LastAuthoritativeDropped = itemId;
            LastAuthoritativeDroppedSeq++;
            return;
        }


        // 드롭 트랜스폼 결정(가능하면 플레이어의 장착된 CarryArm 드롭 포인트 우선)
        Transform dropPoint = this.transform;
        ArmsManager am = GetComponentInChildren<ArmsManager>();
        if (am != null && am.CurrentArm is CarryArm carryArm)
        {
            dropPoint = carryArm.GetCarryPoint();
            if (dropPoint == null) dropPoint = this.transform;
        }

        //Debug.Log("RPC Drop Start");
        // 권한 있는 드롭 수행
        carryable.Drop(dropPoint);

        //Debug.Log("RPC Drop Finish");

        // 서버의 CarryArm(있다면)에 알림을 보내 서버 측 암 상태를 동기화
        if (am != null && am.CurrentArm is CarryArm serverCarryArm)
        {
            serverCarryArm.AuthoritativeDropped(itemId);
        }

        LastAuthoritativeDropped = itemId;
        LastAuthoritativeDroppedSeq++;
        //Debug.Log($"LastAuthoritativeDropped SET : {itemId}, seq={LastAuthoritativeDroppedSeq}");

    }

    public static void ClearPendingQueues()
    {
        pendingPicks.Clear();
        pendingDrops.Clear();
    }

}
