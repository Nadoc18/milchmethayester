using TMPro;
using UnityEngine;
using MNLTHII;
using UnityEngine.UI;
using MNLTHII.Managers;
using System.Linq;

public class PanelPawnDataController : MonoBehaviour
{
    public PawnController m_pawnController;

    [Header("------- UI References ---------")]
    public TextMeshProUGUI energyValueText;
    public Image progressBar;
    public CanvasGroup canvasAlpha;

    private float m_ValueBarLerp;
    private int m_valueTxtLerp;

    private void Update()
    {
        if (m_pawnController == null) return;

        UpdateUIRefs();

        if (GameManager.instance != null && GameManager.instance.hexClicked != null && 
            GameManager.instance.hexClicked.positionInTheBoard.CompareHexCoord(m_pawnController.hexcoord))
        {
            if (canvasAlpha != null) canvasAlpha.alpha = 1;
        }
        else
        {
            if (canvasAlpha != null) canvasAlpha.alpha = 0;
        }

        float maxHP = (m_pawnController.maxHP == 0) ? 100 : m_pawnController.maxHP;
        m_ValueBarLerp = Mathf.Lerp(m_ValueBarLerp, (float)m_pawnController.currentHP / maxHP, Time.deltaTime * 5);
        if (progressBar != null) progressBar.fillAmount = m_ValueBarLerp;

        m_valueTxtLerp = (int)Mathf.Lerp(m_valueTxtLerp, m_pawnController.currentHP, Time.deltaTime * 5);
        if (energyValueText != null) energyValueText.text = m_valueTxtLerp.ToString();
    }

    private void UpdateUIRefs()
    {
        if (energyValueText == null)
        {
            Transform t1 = m_pawnController.transform.Find("PanelHexData");
            if (t1 != null) {
                Transform t2 = t1.Find("HexagonDataBckgrnd");
                if (t2 != null) {
                    Transform t3 = t2.Find("Energy");
                    if (t3 != null) {
                        Transform t4 = t3.Find("Value");
                        if (t4 != null) energyValueText = t4.GetComponent<TextMeshProUGUI>();
                    }
                    Transform p = t2.Find("ProgressBar");
                    if (p != null) progressBar = p.GetComponent<Image>();
                }
                canvasAlpha = t1.GetComponent<CanvasGroup>();
            }
        }
    }
}