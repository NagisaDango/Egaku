using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.Events;

public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private GameObject parent;
    [SerializeField] private TMP_Text tutorialText;
    private bool triggered = false;
    [SerializeField] private TutorialTrigger closeTrigger;
    [SerializeField] private DestroyObserve destroyObj;
    public UnityEvent onTrigger;
    public UnityEvent onObserveObjDestroyed;
    [SerializeField] private LayerMask layerMask;
    [Header("Animation")]
    [SerializeField] private GameObject controllingAnim;
    [SerializeField] private Transform startPos;
    [SerializeField] private Transform endPos;
    [SerializeField] private float animDuration;


    private void Start()
    {
        if(controllingAnim)
            controllingAnim.transform.localPosition = startPos.localPosition;
        if(closeTrigger)
            closeTrigger.onTrigger.AddListener(Close);
        if(destroyObj)
            destroyObj._OnDestroy += () => onObserveObjDestroyed?.Invoke();
    }

    public void MoveGO()
    {
        if(controllingAnim == null)
            return;
        
        DOTween.To(
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
        
        onTrigger?.Invoke();
        
        if (parent != null) 
            parent.SetActive(true);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (((1 << collision.gameObject.layer) & layerMask) != 0)
        {
            TriggerEvent();
        }
    }

    private void Close()
    {
        this.gameObject.SetActive(false);
    }
    
    public void SetText(string text)
    {
        tutorialText.text = text;
    }

    public void DisableObj(GameObject obj)
    {
        obj.SetActive(false);
    }

    public void EnableObj(GameObject obj)
    {
        obj.SetActive(true);
    }
}
