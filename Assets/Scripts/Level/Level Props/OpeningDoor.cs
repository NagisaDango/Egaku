using DG.Tweening;
using UnityEngine;

public class OpeningDoor : MonoBehaviour, IControlObj
{
    public void Activate()
    {
        DOTween.To(
                ()=> this.transform.localScale,
                scale => this.transform.localScale = scale,
                new Vector3(this.transform.localScale.x, 0),
                1f).SetEase(Ease.Linear)
            .OnComplete(() => { this.GetComponent<Collider2D>().enabled = false; });
    }

    public void Deactivate()
    {
        DOTween.To(
                ()=> transform.localScale,
                scale => transform.localScale = scale,
                new Vector3(transform.localScale.x, 1),
                1f).SetEase(Ease.Linear)
            .OnComplete(() => { GetComponent<Collider2D>().enabled = true; });
    }
}
