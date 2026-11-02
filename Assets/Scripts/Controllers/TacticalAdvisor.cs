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
            if (promptText != null) promptText.text = "Selectionnez une case pour recevoir un conseil tactique.";
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
            promptText.text = "MENACE IMMINENTE : Un ennemi est tres proche ! Produisez un Tank en urgence pour vous defendre.";
            return;
        }

        // 2. Check Hex Type specifics
        if (hex.type == TypeOfHex.mountain)
        {
            promptText.text = "STRATEGIE : Les Montagnes generent plus d'energie bonus. Gardez-les sous votre controle !";
            return;
        }
        
        if (hex.type == TypeOfHex.crystal)
        {
            promptText.text = "STRATEGIE : Les Cristaux reduisent le cout d'invocation sur les cases adjacentes.";
            return;
        }

        // 3. General advice
        if (hex.level == 0)
        {
            promptText.text = "TACTIQUE : Cette case est niveau 0. Accumulez des points de connaissance (en repondant aux questions) pour l'ameliorer.";
        }
        else
        {
            promptText.text = "TACTIQUE : Case securisee. Continuez d'etendre votre controle sur le plateau.";
        }
    }
}