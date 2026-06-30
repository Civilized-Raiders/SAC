using Fusion;
using UnityEngine;

/* =========================================================
 * [수정 이력]
 * 2026-05-26 : Carry 스크립트 작성 (NetworkBehaviour 적용)
 * 2026-05-28 : RequestStateAuthority 비동기 처리 지연 대기 로직 추가
 * 2026-05-28 : 클라이언트(Client) 원활한 작동을 위한 권한 제한 우회 추가
 * 2026-06-01 : IsCarrying 속성 추가 (배터리 등의 방전 로직용)
 * 2026-06-01 : 로컬 클릭 이벤트(Update) 활용으로 물리 입력 씹힘 현상 우회
 * 2026-06-01 : 벽/바닥 뚫림 방지를 위해 들고 있는 위치(leftHand) 및 
 *              드롭 위치(dropPoint) 동적 푸시백(Push-Back) 로직 추가
 * 2026-06-05 : OutlineTool 연동 - 들 수 있는 대상(Carryable) 지정 시 아웃라인 표시 및 해제 추가
 * ========================================================= */

public class Carry : NetworkBehaviour
{
    [Header("Carry Settings")]
    [Tooltip("물건이 붙을 왼쪽 팔/집게의 위치")]
    [SerializeField] private Transform leftHand;
    [Tooltip("물건을 내려놓을 때 떨어뜨릴 기준 위치")]
    [SerializeField] private Transform dropPoint;

    private Carryable currentTarget;
    private Carryable carriedItem;

    // 권한을 요청하고 부여받기까지 기다리는 아이템
    private Carryable pendingPickUpItem;

    // 💡 벽/바닥 뚫림 방지 계산에 쓸 최초 상대적 오리진 위치 기억용
    private Vector3 initialLeftHandLocalPos;
    private Vector3 initialDropPointLocalPos;

    // 외부(PlayerMove 등)에서 방전 계산을 위해 물건을 들고 있는지 체크할 수 있는 프로퍼티
    public bool IsCarrying => carriedItem != null;

    private void Awake()
    {
        if (leftHand != null) initialLeftHandLocalPos = leftHand.localPosition;
        if (dropPoint != null) initialDropPointLocalPos = dropPoint.localPosition;
    }

    public override void FixedUpdateNetwork()
    {
        // 💡 1. 캐리 중 벽 뚫림 및 드랍 시 바닥 뚫림 방지 (위치 동적 보정)
        AdjustPositionsToAvoidClipping();

        // 💡 2. 대기 중인 아이템이 권한을 얻으면 즉시 인벤토리에 넣습니다.
        if (pendingPickUpItem != null)
        {
            if (pendingPickUpItem.HasStateAuthority || !pendingPickUpItem.Object.IsValid)
            {
                carriedItem = pendingPickUpItem;
                carriedItem.PickUp(leftHand, Object);
                pendingPickUpItem = null;
            }
        }
    }

    void Update()
    {
        // 네트워크 객체이면서 내가 조종하는 캐릭터가 아닐 때만 입력 차단 (즉, 로컬 본인은 허용)
        if (Object != null && Object.IsValid)
        {
            if (!HasInputAuthority) return;
        }

        // 본인 화면(InputAuthority)이거나 싱글(오프라인)일 때 로컬 클릭 감지
        if (Input.GetMouseButtonDown(0))
        {
            if (carriedItem == null && pendingPickUpItem == null) TryPickUp();
            else if (carriedItem != null) TryDrop();
        }
    }

    /// <summary>
    /// 플레이어 몸 안에서 팔 및 드랍 위치까지 레이캐스트를 쏘아
    /// 벽이나 바닥 안에 위치가 할당되는 것을 막고 살짝 당겨줍니다.
    /// </summary>
    private void AdjustPositionsToAvoidClipping()
    {
        if (leftHand == null || dropPoint == null) return;

        // 레이캐스트 시작점 (플레이어 중심에서 약간 위쪽)
        Vector3 origin = transform.position + Vector3.up * 0.7f;

        // 내가 들고 있는 물건 자체가 레이캐스트에 걸리면 안 되므로 해당 레이어 제외
        int layerMask = ~(1 << LayerMask.NameToLayer("CarriedItem"));

        // --- A. 왼팔(들고 있는 위치) 밀어내기 ---
        Vector3 targetHand = transform.TransformPoint(initialLeftHandLocalPos);
        Vector3 dirHand = targetHand - origin;
        if (Physics.SphereCast(origin, 0.4f, dirHand.normalized, out RaycastHit hitHand, dirHand.magnitude, layerMask, QueryTriggerInteraction.Ignore))
        {
            // 나 자신(탱크 콜라이더 본체)이 아닐 때만 당기기 적용
            if (hitHand.collider.transform.root != transform.root)
                leftHand.position = hitHand.point + hitHand.normal * 0.3f;
            else
                leftHand.localPosition = initialLeftHandLocalPos;
        }
        else
        {
            leftHand.localPosition = initialLeftHandLocalPos;
        }

        // --- B. 드롭 위치 보정 (동일하게 벽 뚫림 방지) ---
        Vector3 targetDrop = transform.TransformPoint(initialDropPointLocalPos);
        Vector3 dirDrop = targetDrop - origin;
        if (Physics.SphereCast(origin, 0.4f, dirDrop.normalized, out RaycastHit hitDrop, dirDrop.magnitude, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (hitDrop.collider.transform.root != transform.root)
                dropPoint.position = hitDrop.point + hitDrop.normal * 0.4f;
            else
                dropPoint.localPosition = initialDropPointLocalPos;
        }
        else
        {
            dropPoint.localPosition = initialDropPointLocalPos;
        }

        // --- C. 드롭 위치 바닥 뚫림 강제 방지 (위에서 아래로 확인) ---
        if (Physics.Raycast(dropPoint.position + Vector3.up * 1f, Vector3.down, out RaycastHit groundHit, 2f, layerMask, QueryTriggerInteraction.Ignore))
        {
            // 드롭 위치가 땅바닥 바로 위 최소 간격보다 낮을 경우
            if (dropPoint.position.y <= groundHit.point.y + 0.1f)
            {
                dropPoint.position = new Vector3(dropPoint.position.x, groundHit.point.y + 0.3f, dropPoint.position.z);
            }
        }
    }

    private void TryPickUp()
    {
        if (currentTarget != null && leftHand != null)
        {
            // 💡 [추가] 대상을 집어들기로 결정했으므로 아웃라인 해제
            if (HasInputAuthority || Object == null || !Object.IsValid)
            {
                OutlineTool.Hide(currentTarget.gameObject);
            }

            NetworkObject carryNetObj = currentTarget.GetComponent<NetworkObject>();

            if (carryNetObj != null && carryNetObj.IsValid)
            {
                if (carryNetObj.HasStateAuthority)
                {
                    carriedItem = currentTarget;
                    carriedItem.PickUp(leftHand, Object);
                }
                else
                {
                    RPC_RequestPickUp(carryNetObj.Id);
                    pendingPickUpItem = currentTarget;
                }
            }
            else
            {
                carriedItem = currentTarget;
                carriedItem.PickUp(leftHand, Object);
            }

            currentTarget = null;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestPickUp(NetworkId itemId)
    {
        if (Runner.TryFindObject(itemId, out NetworkObject obj))
        {
            Carryable carryable = obj.GetComponent<Carryable>();
            if (carryable != null) carryable.PickUp(leftHand, Object);
        }
    }



    private void TryDrop()
    {
        if (carriedItem != null)
        {
            carriedItem.Drop(dropPoint);
            carriedItem = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (carriedItem != null || pendingPickUpItem != null) return;

        Carryable carryable = other.GetComponentInParent<Carryable>();
        if (carryable != null && carryable != currentTarget)
        {
            // 💡 [추가] 이미 타겟이 있었다면 기존 타겟의 아웃라인을 해제
            if (currentTarget != null)
            {
                if (HasInputAuthority || Object == null || !Object.IsValid)
                {
                    OutlineTool.Hide(currentTarget.gameObject);
                }
            }

            currentTarget = carryable;

            // 💡 [추가] 새로 지정된 대상의 아웃라인 활성화 (나만의 화면에서 노란색)
            if (HasInputAuthority || Object == null || !Object.IsValid)
            {
                OutlineTool.Show(currentTarget.gameObject, OutlineTool.OutlineColorType.Yellow);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Carryable carryable = other.GetComponentInParent<Carryable>();
        if (carryable != null && currentTarget == carryable)
        {
            // 💡 [추가] 대상 밖으로 벗어났으므로 아웃라인 해제
            if (HasInputAuthority || Object == null || !Object.IsValid)
            {
                OutlineTool.Hide(currentTarget.gameObject);
            }
            currentTarget = null;
        }
    }
}