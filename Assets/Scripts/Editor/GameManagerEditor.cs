using UnityEditor;
using UnityEngine;

namespace MNLTHII
{
    [CustomEditor(typeof(GameManager))]
    public class GameManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            //Called whenever the inspector is drawn for this object.
            DrawDefaultInspector();

            if (GUILayout.Button(" Prompt "))
            {
                GameManager _gameManager = target as GameManager;

                _gameManager.UpdateTurn(_gameManager.jsonPrompt);
            }
            if (GUILayout.Button(" Timer "))
            {
                GameManager _gameManager = target as GameManager;

                _gameManager.GetTimer(_gameManager.timeInSeconds);
            }
        }
    }
}
