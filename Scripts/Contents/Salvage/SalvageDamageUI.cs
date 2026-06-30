using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class SalvageDamageUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    private Camera _camera;
    public float destroyTime = 3;
    private void Start()
    {
        StartCoroutine(SetCamera());
        _text.color = Color.red;
    }

    public void ShowDamageUI(int damage)
    {
        _text.text = $"${damage}";
        StartCoroutine(Destroy());
    }


    private IEnumerator Destroy()
    {
         yield return new WaitForSeconds(destroyTime);
         Destroy(gameObject);
    }
    
    public void LateUpdate()
    {
        if (_camera.IsNull())
            return;
        transform.LookAt(_camera.transform);
    }
    private IEnumerator SetCamera()
    {
        while (true)
        {
            yield return new WaitForSeconds(1);
            if (PlayerInvisibleController.main.IsNull()) 
                continue;
            _camera = PlayerInvisibleController.main.ownerCam;
            break;
        }
    }
}
