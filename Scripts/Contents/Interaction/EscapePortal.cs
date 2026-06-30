using Fusion;
using System.Collections.Generic;
using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-06-27 : 탈출 방식 변경
 *              - 자동 탈출 제거, E키 수동 탈출
 *              - 범위 안 전원 MarkEscaped(), 범위 밖 전원 Die()
 * 2026-06-27 : 범위 판정 수정
 *              - bounds.Contains() 제거 → OnTriggerEnter/Exit HashSet 추적
 *              - EscapeTrigger(자식 오브젝트) 콜라이더 기준으로 판정
 *              - EscapePortalInteractable은 InteractTrigger(별도 자식)에 분리
 * ========================================================= */

public class EscapePortal : NetworkBehaviour
{
    [Networked] public NetworkBool IsPortalActive { get; set; }

    [SerializeField] private GameObject portalVisual;

    // Inspector에서 EscapeTrigger 자식 오브젝트의 Collider를 연결
    [SerializeField] private Collider escapeTriggerCollider;

    private ChangeDetector _changeDetector;
    private bool _gameEndTriggered;
    private EscapePortalInteractable _interactable;

    // EscapeTrigger OnTriggerEnter/Exit로 추적
    private readonly HashSet<PlayerRef> _playersInRange = new();

    public override void Spawned()
    {
        Debug.Log($"[EscapePortal] Spawned! HasStateAuthority={HasStateAuthority}");

        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        SetVisual(IsPortalActive);

        _interactable = GetComponentInChildren<EscapePortalInteractable>();
        if (_interactable != null)
            _interactable.OnInteracted += RequestEscape;
        else
            Debug.LogWarning("[EscapePortal] EscapePortalInteractable을 찾을 수 없습니다.");

        if (HasStateAuthority)
        {
            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.OnClear += Activate;
            else
                Debug.LogWarning("[EscapePortal] NetworkGameManager.Instance null. FixedUpdateNetwork 직접 체크로 대응.");
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_interactable != null)
            _interactable.OnInteracted -= RequestEscape;

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.OnClear -= Activate;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (!IsPortalActive && NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsCleared)
        {
            Debug.Log("[EscapePortal] IsCleared 감지 → Activate()");
            Activate();
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsPortalActive))
                SetVisual(IsPortalActive);
        }
    }

    // EscapeTrigger 자식의 EscapeRangeTrigger 컴포넌트가 호출
    public void OnPlayerEnterRange(PlayerDeathHandler handler)
    {
        if (!HasStateAuthority)
            return;

        if (handler == null || handler.Object == null || !handler.Object.IsValid)
            return;

        _playersInRange.Add(handler.Object.InputAuthority);

        Debug.Log($"ENTER {handler.Object.InputAuthority}");
    }

    public void OnPlayerExitRange(PlayerDeathHandler handler)
    {
        if (!HasStateAuthority)
            return;

        if (handler == null || handler.Object == null || !handler.Object.IsValid)
            return;

        _playersInRange.Remove(handler.Object.InputAuthority);

        Debug.Log($"EXIT {handler.Object.InputAuthority}");
    }

    private void RequestEscape()
    {
        Debug.Log("Escape Requested");

        if (!IsPortalActive)
            return;

        RpcTriggerEscape();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcTriggerEscape()
    {
        if (!IsPortalActive) return;
        if (_gameEndTriggered) return;

        _gameEndTriggered = true;

        PlayerDeathHandler[] all = FindObjectsByType<PlayerDeathHandler>(FindObjectsSortMode.None);
        bool anyEscaped = false;

        foreach (var handler in all)
        {
            if (handler == null || handler.Object == null || !handler.Object.IsValid) continue;
            if (handler.IsDead || handler.HasEscaped) continue;

            bool inRange = _playersInRange.Contains(handler.Object.InputAuthority);

            Debug.Log($"{handler.Object.InputAuthority} InRange={inRange}");

            if (inRange)
            {
                handler.MarkEscaped();
                anyEscaped = true;
            }
            else
            {
                handler.Die();
            }
        }

        NetworkGameManager.Instance?.TriggerGameEnd(anyEscaped);
    }

    private void Activate()
    {
        if (!HasStateAuthority) return;
        IsPortalActive = true;

        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.RemainingTimeSeconds > 60)
            NetworkGameManager.Instance.RemainingTimeSeconds = 60;

        UIEventManager.TriggerShowToast("탈출 포탈이 활성화되었습니다!");
    }

    private void SetVisual(bool active)
    {
        if (portalVisual != null)
            portalVisual.SetActive(active);
        else
            Debug.LogWarning("[EscapePortal] portalVisual이 연결되지 않았습니다.");
    }
}