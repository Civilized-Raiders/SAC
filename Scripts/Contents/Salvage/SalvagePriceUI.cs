using System.Collections;
using TMPro;
using UnityEngine;

public class SalvagePriceUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    private Camera _camera;

    public Transform target;
    public float minSpeed = 1f;
    public float maxSpeed = 100f;
    public float maxDistance = 20f;

    private void Start()
    {
        _text.color = Color.green;
        StartCoroutine(SetCamera());
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
    public void LateUpdate()
    {
        if (_camera.IsNull())
            return;
        transform.LookAt(_camera.transform);
        FollowTarget();
    }
    
    private void FollowTarget()
    {
        if (false == target) 
            return;

        var distance = Vector3.Distance(transform.position, target.position);

        // 거리가 멀수록 speed가 maxSpeed에 가까워짐
        var t = Mathf.Clamp01(distance / maxDistance);
        var speed = Mathf.Lerp(minSpeed, maxSpeed, t);

        transform.position = Vector3.Lerp(
            transform.position,
            target.position,
            speed * Time.deltaTime
        );
    }
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    public void SetPrice(int salvageGold)
    {
        _text.text = $"${salvageGold}";
    }
    public void SnapToTarget()
    {
        if (target != null)
            transform.position = target.position;

        if (_camera == null && PlayerInvisibleController.main != null)
            _camera = PlayerInvisibleController.main.ownerCam;

        if (_camera != null)
            transform.LookAt(_camera.transform);
    }
}