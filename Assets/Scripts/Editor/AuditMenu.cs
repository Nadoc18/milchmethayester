using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MNLTHII.Dev;

namespace MNLTHII.EditorTools
{
    /// <summary>
    /// Milchemet > Tests : l'audit general (le jeu se joue seul et se surveille, voir
    /// GameAudit) et les tests unitaires (fenetre Test Runner, dossier Editor/Tests).
    /// </summary>
    public static class AuditMenu
    {
        private const string GameScene = "Assets/Scenes/Game.unity";

        [MenuItem("Milchemet/Tests/Audit general - une partie (40 tours)", false, 50)]
        private static void AuditOne()
        {
            Launch(1, 40);
        }

        [MenuItem("Milchemet/Tests/Audit general - une partie longue (80 tours)", false, 51)]
        private static void AuditOneLong()
        {
            Launch(1, 80);
        }

        [MenuItem("Milchemet/Tests/Audit general - toutes les configurations (12 parties)", false, 52)]
        private static void AuditAll()
        {
            Launch(2, 40);
        }

        [MenuItem("Milchemet/Tests/Arreter l'audit", false, 53)]
        private static void StopAudit()
        {
            PlayerPrefs.SetInt(GameAudit.PrefKey, 0);
            PlayerPrefs.Save();
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Debug.Log("[Audit] Arrete.");
        }

        [MenuItem("Milchemet/Tests/Ouvrir le dernier rapport", false, 54)]
        private static void OpenReport()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Audit/rapport_audit.txt"));
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Audit", "Aucun rapport pour l'instant.\nLance d'abord Milchemet > Tests > Audit general.", "OK");
                return;
            }
            EditorUtility.OpenWithDefaultApp(path);
        }

        [MenuItem("Milchemet/Tests/Tests unitaires (fenetre Test Runner)", false, 70)]
        private static void OpenTestRunner()
        {
            if (!EditorApplication.ExecuteMenuItem("Window/General/Test Runner"))
                Debug.LogWarning("[Tests] Fenetre Test Runner introuvable : Window > General > Test Runner, onglet EditMode, Run All.");
        }

        private static void Launch(int mode, int turns)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Audit] Sors d'abord du mode Play.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (EditorSceneManager.GetActiveScene().path != GameScene)
            {
                if (!File.Exists(GameScene))
                {
                    Debug.LogError("[Audit] Scene introuvable : " + GameScene);
                    return;
                }
                EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            }

            PlayerPrefs.SetInt(GameAudit.PrefKey, mode);
            PlayerPrefs.SetInt(GameAudit.PrefTurnsKey, turns);
            PlayerPrefs.Save();

            Debug.Log("[Audit] Lancement : " + (mode == 2 ? "toutes les configurations" : "une partie")
                      + ", " + turns + " tours au plus par partie. Le mode Play s'arretera tout seul ; "
                      + "le rapport sera dans Audit/rapport_audit.txt.");

            EditorApplication.isPlaying = true;
        }
    }
}
