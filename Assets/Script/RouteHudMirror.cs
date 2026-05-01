using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class RouteHudMirror : MonoBehaviour
{
    [SerializeField] private TMP_Text sourceRemainingText;
    [SerializeField] private TMP_Text sourceStatusText;
    [SerializeField] private TMP_Text sourceEtaText;
    [SerializeField] private TMP_Text targetRemainingText;
    [FormerlySerializedAs("targetStatusText")]
    [FormerlySerializedAs("targetHeadingText")]
    [SerializeField] private TMP_Text targetStatusText;
    [SerializeField] private TMP_Text targetEtaText;

    public void SetSources(
        TMP_Text sourceRemaining,
        TMP_Text sourceStatus,
        TMP_Text sourceEta,
        TMP_Text targetRemaining,
        TMP_Text targetHeading,
        TMP_Text targetEta)
    {
        sourceRemainingText = sourceRemaining;
        sourceStatusText = sourceStatus;
        sourceEtaText = sourceEta;
        targetRemainingText = targetRemaining;
        targetStatusText = targetHeading;
        targetEtaText = targetEta;
    }

    private void LateUpdate()
    {
        if (sourceRemainingText != null && targetRemainingText != null)
            targetRemainingText.text = sourceRemainingText.text;

        if (sourceStatusText != null && targetStatusText != null)
            targetStatusText.text = sourceStatusText.text;

        if (sourceEtaText != null && targetEtaText != null)
            targetEtaText.text = sourceEtaText.text;
    }
}
