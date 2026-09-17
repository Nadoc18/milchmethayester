using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MNLTHII.Managers;
using MNLTHII;

public class GlobalPawnDataPanel : MonoBehaviour
{
    public static GlobalPawnDataPanel Instance;

    [Header("------- UI References ---------")]
    public TextMeshProUGUI energyValueText;
    public Image progressBar;
    public CanvasGroup canvasAlpha;

    [Header("------- Settings ---------")]
    public Vector3 offset = new Vector3(0, 3f, 0);
    public float smoothSpeed = 10f;

    private PawnController m_activePawn;
    private float m_ValueBarLerp;
    private int m_valueTxtLerp;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Update()
    {
        // Find active pawn
        m_activePawn = null;
        if (GameManager.instance != null && GameManager.instance.hexClicked != null)
        {
            m_activePawn = GameManager.instance.hexClicked.GetComponentInChildren<PawnController>();
        }

        if (m_activePawn != null)
        {
            if (canvasAlpha != null) canvasAlpha.alpha = Mathf.Lerp(canvasAlpha.alpha, 1f, Time.deltaTime * 10f);
            
            // Follow the pawn smoothly
            Vector3 targetPos = m_activePawn.transform.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);

            // Look at camera
            if (Camera.main != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }

            // Update stats
            float maxHP = (m_activePawn.maxHP == 0) ? 100 : m_activePawn.maxHP;
            m_ValueBarLerp = Mathf.Lerp(m_ValueBarLerp, (float)m_activePawn.currentHP / maxHP, Time.deltaTime * 5);
            if (progressBar != null) progressBar.fillAmount = m_ValueBarLerp;

            m_valueTxtLerp = (int)Mathf.Lerp(m_valueTxtLerp, m_activePawn.currentHP, Time.deltaTime * 5);
            if (energyValueText != null) energyValueText.text = m_valueTxtLerp.ToString();
        }
        else
        {
            if (canvasAlpha != null) canvasAlpha.alpha = Mathf.Lerp(canvasAlpha.alpha, 0f, Time.deltaTime * 10f);
        }
    }
}