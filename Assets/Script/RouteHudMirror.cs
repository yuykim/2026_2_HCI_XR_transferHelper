using TMPro;
using UnityEngine;

public sealed class RouteHudMirror : MonoBehaviour
{
    [SerializeField] private TMP_Text sourceRemainingText;
    [SerializeField] private TMP_Text sourceStatusText;
    [SerializeField] private TMP_Text targetRemainingText;
    [SerializeField] private TMP_Text targetStatusText;

    public void SetSources(
        TMP_Text sourceRemaining,
        TMP_Text sourceStatus,
        TMP_Text targetRemaining,
        TMP_Text targetStatus)
    {
        sourceRemainingText = sourceRemaining;
        sourceStatusText = sourceStatus;
        targetRemainingText = targetRemaining;
        targetStatusText = targetStatus;
    }

    private void LateUpdate()
    {
        if (sourceRemainingText != null && targetRemainingText != null)
            targetRemainingText.text = sourceRemainingText.text;

        if (sourceStatusText != null && targetStatusText != null)
            targetStatusText.text = sourceStatusText.text;
    }
}
