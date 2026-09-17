using UnityEngine;
using MNLTHII;
using System;
using MNLTHII.Managers;

namespace MNLTHII.Factories
{
    public static class PawnFactory
    {
                /// <summary>
        /// Recupere le PawnController du modele instancie. Si le prefab n'en porte pas
        /// (mauvaise entree dans m_HexagonPrefabs, modele decoratif...), on l'ajoute au
        /// vol pour que la partie continue, et on signale l'index a corriger.
        /// </summary>
        private static PawnController EnsurePawnController(GameObject instance, int prefabIndex, TypeOfPawn pawnType)
        {
            if (instance == null) return null;

            PawnController controller = instance.GetComponent<PawnController>();
            if (controller != null) return controller;

            Debug.LogWarningFormat(
                "[PawnFactory] Le prefab d'index {0} de m_HexagonPrefabs (utilise pour {1}) n'a pas de PawnController. " +
                "Composant ajoute automatiquement : verifie cette entree dans le BoardController.",
                prefabIndex, pawnType);

            return instance.AddComponent<PawnController>();
        }

        public static PawnController CreateNewPawn(HexagonData hexData, GameObject[] hexagonPrefabs, Func<HexCoord, Hexagon> getHexByCoord)
        {
            TypeOfPawn pawnType;
            Enum.TryParse(hexData.type, out pawnType);
            int[] pawnPrefabsPath = PrefabsPath.GetPawnPrefabs(pawnType);
            
            if (pawnPrefabsPath == null || pawnPrefabsPath.Length == 0) return null;

            int levelIndex = hexData.level - 1;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex >= pawnPrefabsPath.Length) levelIndex = pawnPrefabsPath.Length - 1;

                        int prefabIndex = pawnPrefabsPath[levelIndex];
            if (prefabIndex < 0) prefabIndex = 0;
            if (prefabIndex >= hexagonPrefabs.Length) prefabIndex = hexagonPrefabs.Length - 1;
            if (hexagonPrefabs[prefabIndex] == null)
            {
                Debug.LogErrorFormat("[PawnFactory] m_HexagonPrefabs[{0}] est vide : impossible de creer {1}.", prefabIndex, pawnType);
                return null;
            }
            GameObject pawnPrefabObj = GameObject.Instantiate(hexagonPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);

            AudioSource audioSource = pawnPrefabObj.AddComponent<AudioSource>();
            audioSource.loop = false;

            if (hexData.type == "enemy")
            {
                if (FXManager.Instance != null) FXManager.Instance.PlayEnemySpawnerSFX(audioSource);
            }
            else
            {
                if (FXManager.Instance != null) FXManager.Instance.PlayUnitSpawnerSFX(audioSource);
            }

            Hexagon hexFrom = getHexByCoord(hexData.from);

            if (hexFrom)
            {
                pawnPrefabObj.transform.position = hexFrom.transform.position;
                pawnPrefabObj.transform.parent = hexFrom.transform;
                if (hexData.type == "enemy")
                {
                    Vector3 relativePos = Vector3.zero - pawnPrefabObj.transform.position;
                    if(relativePos != Vector3.zero) pawnPrefabObj.transform.rotation = Quaternion.LookRotation(relativePos);
                }
            }
            else
            {
                float xPos = (Mathf.Sqrt(3) * hexData.to.q + Mathf.Sqrt(3) / 2 * hexData.to.r) * 0.55f;
                float zPos = -((3.0f / 2 * hexData.to.r) * 0.55f);
                pawnPrefabObj.transform.position = new Vector3(xPos, 0.0f, zPos);
                
                if (hexData.type == "enemy")
                {
                    Vector3 relativePos = Vector3.zero - pawnPrefabObj.transform.position;
                    if(relativePos != Vector3.zero) pawnPrefabObj.transform.rotation = Quaternion.LookRotation(relativePos);
                }
            }

            PawnController pawnController = EnsurePawnController(pawnPrefabObj, prefabIndex, pawnType);
            if (pawnController == null) return null;

            Hexagon hexTo = getHexByCoord(hexData.to);

            pawnController.typeOfPawn = pawnType;
            pawnController.Init(hexData);
            if (hexTo)
                pawnController.target = hexTo.transform;
            pawnController.attacked = hexData.attack_type != null;

            if (FXManager.Instance != null)
            {
                switch (pawnController.typeOfPawn)
                {
                    case TypeOfPawn.unit:
                        FXManager.Instance.SpawnCommunityFX(pawnController.transform.position);
                        break;
                    case TypeOfPawn.enemy:
                        FXManager.Instance.SpawnEnemyFX(pawnController.transform.position);
                        break;
                }
            }

            return pawnController;
        }

        public static PawnController CreateNewPawn(PawnController originalPawn, GameObject[] hexagonPrefabs, Func<HexCoord, Hexagon> getHexByCoord)
        {
            TypeOfPawn pawnType;
            Enum.TryParse(originalPawn.typeOfPawn.ToString(), out pawnType);
            int[] pawnPrefabsPath = PrefabsPath.GetPawnPrefabs(pawnType);

                        int levelIndex = originalPawn.level - 1;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex >= pawnPrefabsPath.Length) levelIndex = pawnPrefabsPath.Length - 1;
            int prefabIndex = pawnPrefabsPath[levelIndex];
            if (prefabIndex < 0) prefabIndex = 0;
            if (prefabIndex >= hexagonPrefabs.Length) prefabIndex = hexagonPrefabs.Length - 1;
            if (hexagonPrefabs[prefabIndex] == null)
            {
                Debug.LogErrorFormat("[PawnFactory] m_HexagonPrefabs[{0}] est vide : impossible de creer {1}.", prefabIndex, pawnType);
                return null;
            }
            GameObject pawnPrefabObj = GameObject.Instantiate(hexagonPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);

            Hexagon hex = getHexByCoord(originalPawn.hexcoord);

            if (hex != null)
            {
                pawnPrefabObj.transform.position = hex.transform.position;
                pawnPrefabObj.transform.parent = hex.transform;
            }

            PawnController pawnController = EnsurePawnController(pawnPrefabObj, prefabIndex, pawnType);
            if (pawnController == null) return null;

            pawnController.typeOfPawn = pawnType;

            return pawnController;
        }
        
        public static PawnController CreateNewPawnAfterErase(HexagonData hexData, GameObject[] hexagonPrefabs, Func<HexCoord, Hexagon> getHexByCoord)
        {
            TypeOfPawn pawnType;
            Enum.TryParse(hexData.type, out pawnType);
            int[] pawnPrefabsPath = PrefabsPath.GetPawnPrefabs(pawnType);
            
                        int levelIndex = hexData.level - 1;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex >= pawnPrefabsPath.Length) levelIndex = pawnPrefabsPath.Length - 1;
            int prefabIndex = pawnPrefabsPath[levelIndex];
            if (prefabIndex < 0) prefabIndex = 0;
            if (prefabIndex >= hexagonPrefabs.Length) prefabIndex = hexagonPrefabs.Length - 1;
            if (hexagonPrefabs[prefabIndex] == null)
            {
                Debug.LogErrorFormat("[PawnFactory] m_HexagonPrefabs[{0}] est vide : impossible de creer {1}.", prefabIndex, pawnType);
                return null;
            }
            GameObject pawnPrefabObj = GameObject.Instantiate(hexagonPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);

            Hexagon hexFrom = getHexByCoord(hexData.from);

            if (hexFrom)
            {
                pawnPrefabObj.transform.position = hexFrom.transform.position;
                pawnPrefabObj.transform.parent = hexFrom.transform;
                if (hexData.type == "enemy")
                {
                    Vector3 relativePos = Vector3.zero - pawnPrefabObj.transform.position;
                    if(relativePos != Vector3.zero) pawnPrefabObj.transform.rotation = Quaternion.LookRotation(relativePos);
                }
            }
            else
            {
                float xPos = (Mathf.Sqrt(3) * hexData.to.q + Mathf.Sqrt(3) / 2 * hexData.to.r) * 0.55f;
                float zPos = -((3.0f / 2 * hexData.to.r) * 0.55f);
                pawnPrefabObj.transform.position = new Vector3(xPos, 0.0f, zPos);
                if (hexData.type == "enemy")
                {
                    Vector3 relativePos = Vector3.zero - pawnPrefabObj.transform.position;
                    if(relativePos != Vector3.zero) pawnPrefabObj.transform.rotation = Quaternion.LookRotation(relativePos);
                }
            }

            PawnController pawnController = EnsurePawnController(pawnPrefabObj, prefabIndex, pawnType);
            if (pawnController == null) return null;

            Hexagon hexTo = getHexByCoord(hexData.to);

            pawnController.typeOfPawn = pawnType;
            pawnController.Init(hexData);
            if (hexTo)
                pawnController.target = hexTo.transform;

            pawnController.attacked = hexData.attack_type != null;

            return pawnController;
        }
    }
}




