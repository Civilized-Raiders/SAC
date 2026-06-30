using System;

/*
 * [UI Event Manager] 5 / 29
 * UI 컴포넌트들이 서로 직접 참조하지 않고, 이벤트를 통해 간접적으로 통신할 수 있도록 하는 매니저 클래스입니다.
 * 예시로, UIToast는 UIEventManager의 OnShowToast 이벤트를 구독하여 메시지를 표시합니다.
 * 다른 UI 컴포넌트들도 필요에 따라 이벤트를 추가하여 사용할 수 있습니다.
 */
public static class UIEventManager
{
    // Toast 이벤트
    public static event Action<string> OnShowToast;

    // Toast 호출
    public static void TriggerShowToast(string message)
    {
        OnShowToast?.Invoke(message);
    }
}