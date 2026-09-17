using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MNLTHII.Managers;
using MNLTHII;

public class GlobalHexDataPanel : MonoBehaviour
{
    public static GlobalHexDataPanel Instance;

    [Header("------- UI References ---------")]
    public TextMeshProUGUI energyValueText;
    public TextMeshProUGUI hexNameText;
    public TextMeshProUGUI hexLevelText;
    public Image progressBar;
    public CanvasGroup canvasAlpha;

    private Hexagon m_activeHex;
    private float m_ValueBarLerp;
    private int m_valueTxtLerp;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Update()
    {
        m_activeHex = null;
        if (GameManager.instance != null && GameManager.instance.hexClicked != null)
        {
            m_activeHex = GameManager.instance.hexClicked;
        }

        if (m_activeHex != null)
        {
            if (canvasAlpha != null) canvasAlpha.alpha = Mathf.Lerp(canvasAlpha.alpha, 1f, Time.deltaTime * 10f);

            float maxHP = (m_activeHex.maxHP == 0) ? 100 : m_activeHex.maxHP;
            m_ValueBarLerp = Mathf.Lerp(m_ValueBarLerp, (float)m_activeHex.currentHP / maxHP, Time.deltaTime * 5);
            if (progressBar != null) progressBar.fillAmount = m_ValueBarLerp;

            m_valueTxtLerp = (int)Mathf.Lerp(m_valueTxtLerp, m_activeHex.currentHP, Time.deltaTime * 5);
            if (energyValueText != null) energyValueText.text = m_valueTxtLerp.ToString();
            
            if (hexNameText != null) hexNameText.text = m_activeHex.type.ToString().ToUpper();
            if (hexLevelText != null) hexLevelText.text = "LEVEL " + m_activeHex.level.ToString();
        }
        else
        {
            if (canvasAlpha != null) canvasAlpha.alpha = Mathf.Lerp(canvasAlpha.alpha, 0f, Time.deltaTime * 10f);
        }
    }
}