using UnityEditor;
using UnityEngine;

namespace MNLTHII.EditorTools
{
    /// <summary>
    /// Interrupteur du realisateur de trailer (voir Dev/TrailerDirector.cs). Il ne
    /// touche qu'une cle PlayerPrefs : rien dans les scenes, rien dans le build.
    /// </summary>
    public static class TrailerMenu
    {
        private const string Key = "mnlth_trailer";

        [MenuItem("Milchemet/Trailer/Activer le realisateur", false, 50)]
        private static void Enable()
        {
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
            Debug.Log("[Trailer] Realisateur ACTIVE. Lance la scene Game : F1..F10 jouent les prises, F12 remet a zero, H cache le HUD.");
        }

        [MenuItem("Milchemet/Trailer/Activer le realisateur", true)]
        private static bool EnableValid() { return PlayerPrefs.GetInt(Key, 0) != 1; }

        [MenuItem("Milchemet/Trailer/Desactiver le realisateur", false, 51)]
        private static void Disable()
        {
            PlayerPrefs.SetInt(Key, 0);
            PlayerPrefs.Save();
            Debug.Log("[Trailer] Realisateur desactive : le jeu redevient normal.");
        }

        [MenuItem("Milchemet/Trailer/Desactiver le realisateur", true)]
        private static bool DisableValid() { return PlayerPrefs.GetInt(Key, 0) == 1; }
    }
}
