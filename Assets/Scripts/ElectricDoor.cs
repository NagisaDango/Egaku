using DG.Tweening;
using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class ElectricDoor : MonoBehaviourPun, IElectricControl
{
    [SerializeField] private GameObject controllingDoor;
    private bool gotBattery;
    public void BatteryIn()
    {
        photonView.RPC("RPC_BatteryIn", RpcTarget.All);
    }

    [PunRPC]
    private void RPC_BatteryIn()
    {
        DOTween.To(
                ()=> controllingDoor.transform.localScale,
                scale => controllingDoor.transform.localScale = scale,
                new Vector3(controllingDoor.transform.localScale.x, 0),
                1f).SetEase(Ease.Linear)
            .OnComplete(() => { controllingDoor.GetComponent<Collider2D>().enabled = false; });
    }

    [PunRPC]
    private void RPC_BatteryOut()
    {
        gotBattery = false;
        DOTween.To(
                ()=> controllingDoor.transform.localScale,
                scale => controllingDoor.transform.localScale = scale,
                new Vector3(controllingDoor.transform.localScale.x, 1),
                1f).SetEase(Ease.Linear)
            .OnComplete(() => { controllingDoor.GetComponent<Collider2D>().enabled = true; });
    }

    public void BatteryOut()
    {
        photonView.RPC("RPC_BatteryOut", RpcTarget.All);
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
