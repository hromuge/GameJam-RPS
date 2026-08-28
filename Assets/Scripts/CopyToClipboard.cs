using UnityEngine;
using TMPro;

public class CopyToClipboard : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyCodeText;

    public void CopyCode()
    {
        if (lobbyCodeText != null)
        {
            // Speichert den Text in der Zwischenablage des Betriebssystems!
            GUIUtility.systemCopyBuffer = lobbyCodeText.text;
            Debug.Log("Code in Zwischenablage kopiert: " + lobbyCodeText.text);
        }
    }
}