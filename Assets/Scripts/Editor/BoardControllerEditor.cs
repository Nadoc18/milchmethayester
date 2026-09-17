using UnityEditor;
using UnityEngine;

namespace MNLTHII
{
    [CustomEditor(typeof(BoardController))]
    public class BoardControllerEditor : Editor
    {
        private int i = 0;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Update board (Load JSON 0)"))
            {
                BoardController _boardController = target as BoardController;
                if (_boardController != null && _boardController.jsonData != null && _boardController.jsonData.Length > 0)
                {
                    _boardController.UpdateBoard(_boardController.jsonData[0]);
                }
            }
            if (GUILayout.Button("Next Json"))
            {
                BoardController _boardController = target as BoardController;
                if (_boardController != null && _boardController.jsonData != null && i < _boardController.jsonData.Length - 1)
                {
                    i++;
                    _boardController.UpdateBoard(_boardController.jsonData[i]);
                }
            }
            if (GUILayout.Button("Reset Jsons"))
            {
                i = 0;
            }
            if (GUILayout.Button("Clear Board"))
            {
                BoardController _boardController = target as BoardController;
                if (_boardController != null)
                {
                    _boardController.ClearMap();
                }
            }
        }
    }
}