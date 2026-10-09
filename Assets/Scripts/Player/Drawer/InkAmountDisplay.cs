using TMPro;
using UnityEngine;

/// <summary>Both roles display the owning Drawer's ink budget; gameplay counters remain untouched.</summary>
public sealed class InkAmountDisplay : MonoBehaviour
{
    [SerializeField] private GameObject inkRoot;
    [SerializeField] private TMP_Text amountText;

    // Cache the rendered budget so drawing updates immediately without rebuilding text every frame.
    private int displayedRemaining = int.MinValue;
    private int displayedMaximum = int.MinValue;
    private UnityEngine.UI.Slider slider;
    private UnityEngine.UI.Image background, fill;
    private Color displayedColor;
    private bool hasDisplayedColor;

    private void Awake()
    {
        // The component lives on GameCanvas so it can reveal the hidden slider once data is ready.
        if (inkRoot == null) return;
        slider = inkRoot.GetComponent<UnityEngine.UI.Slider>();
        background = inkRoot.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
        fill = inkRoot.transform.Find("Fill Area/Fill")?.GetComponent<UnityEngine.UI.Image>();
        inkRoot.SetActive(false);
    }

    private void LateUpdate()
    {
        var drawer = Drawer.Instance;
        bool visible = drawer != null && drawer.SceneReady &&
                       drawer.gameObject.scene == gameObject.scene;
        int remaining = 0, maximum = 0;
        Color color = Color.white;
        visible = visible && drawer.TryGetInkHud(out remaining, out maximum, out color);
        if (inkRoot == null) return;
        if (inkRoot.activeSelf != visible) inkRoot.SetActive(visible);
        if (!visible || amountText == null) return;

        // Keep the Drawer's existing erase-fill animation; the Runner uses authoritative
        // display snapshots instead of its remote copy's potentially different ink counters.
        if (!drawer.photonView.IsMine && slider != null)
            slider.value = maximum < 0 ? 1f : maximum > 0 ? remaining / (float)maximum : 0f;
        if (!hasDisplayedColor || displayedColor != color)
        {
            if (background != null) background.color = new Color(color.r, color.g, color.b, 0.5f);
            if (fill != null) fill.color = new Color(color.r, color.g, color.b, 1f);
            displayedColor = color;
            hasDisplayedColor = true;
        }
        // The eraser has no budget and keeps the last ink pen displayed on both clients.
        if (remaining == displayedRemaining && maximum == displayedMaximum) return;
        displayedRemaining = remaining;
        displayedMaximum = maximum;
        amountText.text = maximum < 0 ? "∞/∞" : $"{remaining}/{maximum}";
    }
}
