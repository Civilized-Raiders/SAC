using Fusion;
using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-05-25 : Interaction 기본 구조 작성
 * 2026-06-05 : 상호작용 대상이 무기(ArmInteractable)일 경우 장착 함수로 연결되도록 추가
 * ========================================================= */

public class Interaction : NetworkBehaviour
{
    [Header("Interaction Settings")]
    private Interactable currentInteractable;

    [Networked]
    private NetworkButtons previousButtons { get; set; }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input)) return;

        if (input.buttons.WasPressed(previousButtons, InputButton.Interact))
        {
            if(currentInteractable != null)
            {
                // 💡 [수정] 무기라면 플레이어 정보를 함께 넘겨주고, 일반 아이템이면 기존 동작 유지
                ArmInteractable armItem = currentInteractable as ArmInteractable;
                if (armItem != null)
                {
                    armItem.InteractWithPlayer(this.gameObject);
                }
                else
                {
                    currentInteractable.Interact();
                }
            }
        }
        previousButtons = input.buttons;
    }

    private void Update()
    {
        if(Object == null || !Object.IsValid)
        {
            if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
            {
                ArmInteractable armItem = currentInteractable as ArmInteractable;
                if (armItem != null)
                {
                    armItem.InteractWithPlayer(this.gameObject);
                }
                else
                {
                    currentInteractable.Interact();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Interactable interactable = other.GetComponent<Interactable>();
        
        if (interactable != null && currentInteractable != interactable)
        {
            currentInteractable = interactable;
            Debug.Log($"상호작용 가능: {other.gameObject.name} (E키를 누르세요)");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Interactable interactable = other.GetComponent<Interactable>();
        
        if (interactable != null && currentInteractable == interactable)
        {
            currentInteractable = null;
            Debug.Log($"상호작용 범위 벗어남: {other.gameObject.name}");
        }
    }
}
