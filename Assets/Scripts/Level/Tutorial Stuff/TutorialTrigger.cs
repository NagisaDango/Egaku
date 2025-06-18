using UnityEngine;
using DG.Tweening;

public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private GameObject controllingAnim;
    [SerializeField] private Transform startPos;
    [SerializeField] private Transform endPos;
    [SerializeField] private float animDuration;

    [SerializeField] private GameObject parent;
    private Tween a;
    private bool triggered = false;

    private void Start()
    {
        controllingAnim.transform.localPosition = startPos.localPosition;
    }

    private void MoveGO()
    {
        a = DOTween.To(
            () => controllingAnim.transform.localPosition,
            pos => controllingAnim.transform.localPosition = pos,
            endPos.localPosition,
            animDuration
        ).SetEase(Ease.Linear)
        .SetLoops(-1, LoopType.Restart);
    }

    private void TriggerEvent()
    {
        if (triggered)
            return;

        triggered = true;
        if (parent != null) 
            parent.SetActive(true);

        if (controllingAnim != null)
        {
            MoveGO();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TriggerEvent();
    }
}
