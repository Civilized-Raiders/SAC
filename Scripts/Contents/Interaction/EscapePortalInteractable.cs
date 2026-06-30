using System;
using UnityEngine;

/* =========================================================
 * EscapePortal의 E키 상호작용을 받는 브릿지 컴포넌트
 * EscapePortal과 같은 GameObject에 추가
 * ========================================================= */


public class EscapePortalInteractable : Interactable
{
    public event Action OnInteracted;
    public override void Interact()
    {
        OnInteracted?.Invoke();
    }
}
