using UnityEngine;
using MNLTHII;

namespace MNLTHII.Factories
{
    public static class HexagonFactory
    {
                public static Hexagon CreateNewHexagon(HexagonData hexData, bool afterDestroyed, GameObject[] hexagonPrefabs, Transform parentTransform)
        {
            int[] hexPrefabsPath = PrefabsPath.GetHexagonPrefabs(hexData.typeID);
            int levelIndex = afterDestroyed ? 0 : hexData.level;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex >= hexPrefabsPath.Length) levelIndex = hexPrefabsPath.Length - 1;
            
            float xPos = (Mathf.Sqrt(3) * hexData.to.q + Mathf.Sqrt(3) / 2 * hexData.to.r) * 0.55f;
            float zPos = -((3.0f / 2 * hexData.to.r) * 0.55f);
            Vector3 position = new Vector3(xPos, 0.0f, zPos);
            Quaternion rotation = Quaternion.Euler(0, 90, 0);

                        int prefabIndex = hexPrefabsPath[levelIndex];
            if (prefabIndex < 0) prefabIndex = 0;
            if (prefabIndex >= hexagonPrefabs.Length) prefabIndex = hexagonPrefabs.Length - 1;
            GameObject hexagonPrefabObj = GameObject.Instantiate(hexagonPrefabs[prefabIndex], position, rotation);
            Hexagon newHexagon = hexagonPrefabObj.AddComponent<Hexagon>();
            newHexagon.Init(hexData, hexagonPrefabObj);
            newHexagon.transform.parent = parentTransform;
            newHexagon.gameObject.AddComponent<AudioSource>();
            return newHexagon;
        }

                public static Hexagon CreateNewHexagon(Hexagon hex, bool afterDestroyed, GameObject[] hexagonPrefabs, Transform parentTransform)
        {
            int[] hexPrefabsPath = PrefabsPath.GetHexagonPrefabs(hex.type);
            int levelIndex = afterDestroyed ? 0 : hex.level;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex >= hexPrefabsPath.Length) levelIndex = hexPrefabsPath.Length - 1;

            float xPos = (Mathf.Sqrt(3) * hex.positionInTheBoard.q + Mathf.Sqrt(3) / 2 * hex.positionInTheBoard.r) * 0.55f;
            float zPos = -((3.0f / 2 * hex.positionInTheBoard.r) * 0.55f);
            Vector3 position = new Vector3(xPos, 0.0f, zPos);
            Quaternion rotation = Quaternion.Euler(0, 90, 0);

                        int prefabIndex = hexPrefabsPath[levelIndex];
            if (prefabIndex < 0) prefabIndex = 0;
            if (prefabIndex >= hexagonPrefabs.Length) prefabIndex = hexagonPrefabs.Length - 1;
            GameObject hexagonPrefabObj = GameObject.Instantiate(hexagonPrefabs[prefabIndex], position, rotation);
            Hexagon newHexagon = hexagonPrefabObj.AddComponent<Hexagon>();
                        newHexagon.level = hex.level;
            newHexagon.type = hex.type;
            newHexagon.positionInTheBoard = hex.positionInTheBoard;
            newHexagon.commandPoints = hex.commandPoints;
            newHexagon.energy = hex.energy;
            // On preserve l'etat de jeu : PV courants, PV max et anciennete (portails).
            newHexagon.currentHP = hex.currentHP;
            newHexagon.maxHP = hex.maxHP;
            newHexagon.energymax = hex.energymax;
            newHexagon.turnsAlive = hex.turnsAlive;
            newHexagon.buildingType = hex.buildingType;

            // LA BULLE D'UN CENTRE DE COMMANDEMENT FAIT PARTIE DE CET ETAT.
            //
            // Changer de modele detruit l'ancien hexagone et en fabrique un neuf. Tout
            // ce qui n'est pas recopie ici est donc PERDU - et la bulle ne l'etait pas.
            // Consequence vue en jeu : on batissait un Centre, il n'avait aucune bulle,
            // et elle n'apparaissait qu'a l'etape des batiments, trois phases plus
            // tard, quand le filet de securite de BuildingManager la reposait. Entre
            // les deux, le Centre ne repoussait rien et n'etait protege par rien.
            newHexagon.shieldHP = hex.shieldHP;
            newHexagon.shieldMax = hex.shieldMax;
            newHexagon.transform.parent = parentTransform;
            newHexagon.gameObject.AddComponent<AudioSource>();
            return newHexagon;
        }

                public static Hexagon CreateDestroyedHexagon(HexagonData hexData, GameObject[] hexagonPrefabs, Transform parentTransform)
        {
            int[] hexDestroyedPrefabsPath = PrefabsPath.GetDestroyedHexagonPrefabs(hexData.typeID);
            int levelIndex = hexData.level;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex >= hexDestroyedPrefabsPath.Length) levelIndex = hexDestroyedPrefabsPath.Length - 1;

            float xPos = (Mathf.Sqrt(3) * hexData.to.q + Mathf.Sqrt(3) / 2 * hexData.to.r) * 0.55f;
            float zPos = -((3.0f / 2 * hexData.to.r) * 0.55f);
            Vector3 position = new Vector3(xPos, 0.0f, zPos);
            Quaternion rotation = Quaternion.Euler(0, 90, 0);

                        int prefabIndex = hexDestroyedPrefabsPath[levelIndex];
            if (prefabIndex < 0) prefabIndex = 0;
            if (prefabIndex >= hexagonPrefabs.Length) prefabIndex = hexagonPrefabs.Length - 1;
            GameObject hexagonPrefabObj = GameObject.Instantiate(hexagonPrefabs[prefabIndex], position, rotation);
            Hexagon newHexagon = hexagonPrefabObj.AddComponent<Hexagon>();
            newHexagon.Init(hexData, hexagonPrefabObj);
            newHexagon.transform.parent = parentTransform;
            newHexagon.energy = 0;
            return newHexagon;
        }

                public static Hexagon CreateDestroyedHexagon(Hexagon hex, GameObject[] hexagonPrefabs, Transform parentTransform)
        {
            int[] hexDestroyedPrefabsPath = PrefabsPath.GetDestroyedHexagonPrefabs(hex.type);
            int levelIndex = hex.level;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex >= hexDestroyedPrefabsPath.Length) levelIndex = hexDestroyedPrefabsPath.Length - 1;

            float xPos = (Mathf.Sqrt(3) * hex.positionInTheBoard.q + Mathf.Sqrt(3) / 2 * hex.positionInTheBoard.r) * 0.55f;
            float zPos = -((3.0f / 2 * hex.positionInTheBoard.r) * 0.55f);
            Vector3 position = new Vector3(xPos, 0.0f, zPos);
            Quaternion rotation = Quaternion.Euler(0, 90, 0);

                        int prefabIndex = hexDestroyedPrefabsPath[levelIndex];
            if (prefabIndex < 0) prefabIndex = 0;
            if (prefabIndex >= hexagonPrefabs.Length) prefabIndex = hexagonPrefabs.Length - 1;
            GameObject hexagonPrefabObj = GameObject.Instantiate(hexagonPrefabs[prefabIndex], position, rotation);
            Hexagon newHexagon = hexagonPrefabObj.AddComponent<Hexagon>();
            newHexagon.level = hex.level;
            newHexagon.type = hex.type;
            newHexagon.positionInTheBoard = hex.positionInTheBoard;
            newHexagon.commandPoints = hex.commandPoints;
            newHexagon.energy = 0;
            newHexagon.transform.parent = parentTransform;

#if UNITY_EDITOR
            newHexagon.BoardCoordforEditor.x = hex.positionInTheBoard.q;
            newHexagon.BoardCoordforEditor.y = hex.positionInTheBoard.r;
            newHexagon.BoardCoordforEditor.z = hex.positionInTheBoard.s;
#endif
            return newHexagon;
        }
    }
}




