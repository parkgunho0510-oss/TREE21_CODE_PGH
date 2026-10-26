using System.Collections;
using UnityEngine;
using Wooduduk.Slot;

public class TutorialFireFedAnimation : MonoBehaviour
{
     private float _targetWorldY = -1f;
     private float _moveSpeed = 0.5f;

    // 1회용 실행을 완벽하게 보장하기 위한 플래그
    private bool _hasTriggered = false; 

    private void OnEnable()
    {
        if (!_hasTriggered)
        {
            SlotEvent.OnFireFed += HandleFireFed;
        }
    }

    private void OnDisable()
    {
        SlotEvent.OnFireFed -= HandleFireFed;
    }

    private void HandleFireFed(RunProgress run)
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        SlotEvent.OnFireFed -= HandleFireFed;

        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        Vector3 targetPosition = new Vector3(transform.position.x, _targetWorldY, transform.position.z);

        while (transform.position != targetPosition)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, _moveSpeed * Time.deltaTime);
            yield return null;
        }

        gameObject.SetActive(false);
    }
}