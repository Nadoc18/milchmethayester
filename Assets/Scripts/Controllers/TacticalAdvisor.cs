using TMPro;
using UnityEngine;
using MNLTHII.Managers;
using MNLTHII;

public class TacticalAdvisor : MonoBehaviour
{
    public TextMeshProUGUI promptText;
    
    private Hexagon lastCheckedHex;
    
    private void Update()
    {
        if (GameManager.instance == null || GameManager.instance.hexClicked == null)
        {
            if (promptText != null) promptText.text = "Sélectionnez une case pour recevoir un conseil tactique.";
            lastCheckedHex = null;
            return;
        }

        Hexagon currentHex = GameManager.instance.hexClicked;
        if (currentHex != lastCheckedHex)
        {
            lastCheckedHex = currentHex;
            AnalyzeAndPrompt(currentHex);
        }
    }

    private void AnalyzeAndPrompt(Hexagon hex)
    {
        if (promptText == null) return;

        // 1. Check for enemies nearby
        bool enemyNearby = false;
        if (BoardController.instance != null && BoardController.instance.PawnsInBoard != null)
        {
            foreach (var pawn in BoardController.instance.PawnsInBoard)
            {
                if (pawn != null && pawn.typeOfPawn == TypeOfPawn.enemy)
                {
                    int dist = BoardController.GetHexDistance(hex.positionInTheBoard, pawn.hexcoord);
                    if (dist > 0 && dist <= 2)
                    {
                        enemyNearby = true;
                        break;
                    }
                }
            }
        }

        if (enemyNearby)
        {
            promptText.text = "MENACE IMMINENTE : Un ennemi est très proche ! Produisez un Tank en urgence pour vous défendre.";
            return;
        }

        // 2. Check Hex Type specifics
        if (hex.type == TypeOfHex.mountain)
        {
            promptText.text = "STRATÉGIE : Les Montagnes génèrent plus d'énergie bonus. Gardez-les sous votre contrôle !";
            return;
        }
        
        if (hex.type == TypeOfHex.crystal)
        {
            promptText.text = "STRATÉGIE : Les Cristaux réduisent le coût d'invocation sur les cases adjacentes.";
            return;
        }

        // 3. General advice
        if (hex.level == 0)
        {
            promptText.text = "TACTIQUE : Cette case est niveau 0. Accumulez des points de connaissance (en répondant aux questions) pour l'améliorer.";
        }
        else
        {
            promptText.text = "TACTIQUE : Case sécurisée. Continuez d'étendre votre contrôle sur le plateau.";
        }
    }
}