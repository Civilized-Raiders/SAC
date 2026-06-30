using UnityEngine;

public class ResetCursor : MonoBehaviour
{
    
    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    
}
