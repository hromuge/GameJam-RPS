using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening; // Für die weichen Fade-Animationen

public class ButtonHoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Settings")]
    [SerializeField] private string hoverMessage; 
    [SerializeField] private TextMeshProUGUI targetTextElement;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetTextElement == null) return;

        // 1. Den Text des Feldes auf die Nachricht dieses Buttons ändern
        targetTextElement.text = hoverMessage;

        // 2. Weich einblenden (DOKill stoppt laufende Animationen, falls man wild mit der Maus wackelt)
        targetTextElement.DOKill();
        targetTextElement.DOFade(1f, 0.2f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetTextElement == null) return;

        // Weich wieder ausblenden, wenn die Maus den Button verlässt
        targetTextElement.DOKill();
        targetTextElement.DOFade(0f, 0.2f);
    }
}