using DG.Tweening;
using UnityEngine;

public class ElectricDoor : MonoBehaviour, IElectricControl
{
    [SerializeField] private GameObject controllingDoor;
    private bool gotBattery;
    public void BatteryIn()
    {
        DOTween.To(
            ()=> controllingDoor.transform.localScale,
            scale => controllingDoor.transform.localScale = scale,
            Vector3.zero,
            1f).SetEase(Ease.Linear)
            .OnComplete(() => { controllingDoor.GetComponent<Collider2D>().enabled = false; });
    }

    public void BatteryOut()
    {
        gotBattery = false;
        DOTween.To(
                ()=> controllingDoor.transform.localScale,
                scale => controllingDoor.transform.localScale = scale,
                Vector3.one,
                1f).SetEase(Ease.Linear)
            .OnComplete(() => { controllingDoor.GetComponent<Collider2D>().enabled = true; });
    }

    public void DetectBattery()
    {
        //throw new System.NotImplementedException();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!gotBattery && other.gameObject.CompareTag("Battery"))
        {
            gotBattery = true;
            other.gameObject.GetComponent<Battery>().ConnectToElectric(this);
            DOTween.To(
                    ()=> other.transform.position,
                    pos => other.transform.position = pos,
                    this.transform.position,
                    0.5f).SetEase(Ease.Linear)
                .OnComplete(BatteryIn);
        }
    }    
}
