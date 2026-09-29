using Photon.Pun;
using UnityEngine;

/// <summary>
/// Scales the Runner's shared pointer only on a Drawer display. RunnerMouse's
/// PhotonTransformView sends position and rotation, never scale.
/// </summary>
public sealed class RunnerMouseLocalSize : MonoBehaviour
{
    private Vector3 authoredScale;
    private bool hasDrawer;
    private bool roleResolved;

    private void Awake()
    {
        authoredScale = transform.localScale;
        EgakuSettings.Changed += Refresh;
    }

    private void OnDestroy() => EgakuSettings.Changed -= Refresh;

    private void Update()
    {
        // The offline Runner may instantiate this object before the local Drawer spawns.
        if (!roleResolved) Refresh();
    }

    private void Refresh()
    {
        if (!roleResolved)
        {
            if (Allan.GameManager.IsLocalMultiplayer)
            {
                hasDrawer = true;
                roleResolved = true;
            }
            else if (PhotonNetwork.LocalPlayer != null &&
                     PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Role", out object role) && role is int roleNumber)
            {
                hasDrawer = roleNumber == (int)RolesManager.PlayerRole.Drawer;
                roleResolved = true;
            }
            else
                foreach (Drawer drawer in FindObjectsByType<Drawer>(FindObjectsSortMode.None))
                    if (drawer.photonView.IsMine) { hasDrawer = true; roleResolved = true; break; }
        }

        transform.localScale = authoredScale * (hasDrawer ? EgakuSettings.RunnerIndicatorScale : 1f);
    }
}
