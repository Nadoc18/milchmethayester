using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MNLTHII.EditorTools
{
    /// <summary>
    /// Detache les systemes de jeu du vieux GameCanvas, pour qu'il puisse etre supprime.
    ///
    /// LE PROBLEME
    ///
    /// TriviaManager vit sur le GameObject GameCanvas. C'est un accident d'histoire :
    /// a l'epoque, tout ce qui touchait a l'interface y etait pose. Mais TriviaManager
    /// n'est pas de l'interface - c'est la banque de questions, le tirage, les regles
    /// de gain. Supprimer GameCanvas emporterait donc le systeme de questions entier,
    /// et la partie sauterait l'etape du revenu sans autre explication qu'un
    /// "aucun TriviaManager dans la scene" dans la console.
    ///
    /// CE QUE FAIT L'OUTIL
    ///
    /// Il cree un objet racine "Milchemet Systems" et y DEPLACE les composants
    /// listes dans Rescued, en preservant toutes leurs valeurs serialisees - y compris
    /// les references d'inspecteur vers les panneaux du HUD.
    ///
    /// Le deplacement passe par ComponentUtility.CopyComponent puis
    /// PasteComponentAsNew, exactement ce que fait le menu "Copy Component / Paste
    /// Component As New" de l'inspecteur. Recreer le composant a la main aurait donne
    /// un composant vierge, et il aurait fallu tout recabler.
    ///
    /// Menu : Milchemet > Detacher les systemes du GameCanvas
    ///
    /// L'outil est sans effet s'il n'y a rien a deplacer, et il refuse de creer un
    /// doublon : deux TriviaManager chargeraient deux fois la banque et se
    /// disputeraient l'instance statique.
    /// </summary>
    public static class SystemsExtractor
    {
        private const string HostName = "Milchemet Systems";

        /// <summary>
        /// Les composants a sauver, par nom de type. On compare sur le nom plutot que
        /// sur le type lui-meme : l'outil continue de fonctionner si l'un d'eux est
        /// renomme ou retire du projet, au lieu de ne plus compiler.
        /// </summary>
        private static readonly string[] Rescued =
        {
            "TriviaManager"
        };

        [MenuItem("Milchemet/Detacher les systemes du GameCanvas", false, 20)]
        public static void Extract()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Systemes] Aucune scene ouverte.");
                return;
            }

            GameObject host = FindOrCreateHost(scene);
            int moved = 0;

            for (int i = 0; i < Rescued.Length; i++)
            {
                string typeName = Rescued[i];

                MonoBehaviour source = FindComponentByName(scene, typeName, host);
                if (source == null)
                {
                    Debug.LogFormat("[Systemes] {0} : rien a deplacer.", typeName);
                    continue;
                }

                if (HasComponentNamed(host, typeName))
                {
                    Debug.LogWarningFormat("[Systemes] {0} existe deja sur \"{1}\" ET sur \"{2}\". "
                                         + "Deux exemplaires chargeraient deux fois la banque de questions "
                                         + "et se disputeraient l'instance statique : supprime celui de "
                                         + "\"{2}\" a la main.",
                                           typeName, HostName, source.gameObject.name);
                    continue;
                }

                string from = source.gameObject.name;

                // Copie fidele : toutes les valeurs serialisees suivent, y compris les
                // references vers les panneaux du HUD deja cablees par le constructeur.
                if (!ComponentUtility.CopyComponent(source))
                {
                    Debug.LogErrorFormat("[Systemes] Impossible de copier {0}.", typeName);
                    continue;
                }

                if (!ComponentUtility.PasteComponentAsNew(host))
                {
                    Debug.LogErrorFormat("[Systemes] Impossible de coller {0} sur \"{1}\".", typeName, HostName);
                    continue;
                }

                Object.DestroyImmediate(source);
                moved++;

                Debug.LogFormat("[Systemes] {0} deplace de \"{1}\" vers \"{2}\", references comprises.",
                                typeName, from, HostName);
            }

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);

            if (moved > 0)
            {
                Debug.LogFormat("[Systemes] {0} composant(s) deplace(s). Verifie leurs references dans "
                              + "l'inspecteur de \"{1}\", puis tu peux supprimer GameCanvas. "
                              + "Enregistre la scene (Ctrl+S).", moved, HostName);
            }
            else
            {
                Debug.Log("[Systemes] Rien n'a bouge.");
            }

            Selection.activeGameObject = host;
        }

        private static GameObject FindOrCreateHost(Scene scene)
        {
            List<GameObject> roots = new List<GameObject>(scene.rootCount);
            scene.GetRootGameObjects(roots);

            for (int i = 0; i < roots.Count; i++)
                if (roots[i] != null && roots[i].name == HostName) return roots[i];

            GameObject created = new GameObject(HostName);
            Debug.LogFormat("[Systemes] Objet \"{0}\" cree a la racine de la scene.", HostName);
            return created;
        }

        /// <summary>
        /// Cherche un composant par nom de type, objets desactives compris - un
        /// TriviaManager sur un objet eteint est justement le cas qu'on veut rattraper.
        /// L'hote est ignore, sinon on se deplacerait sur soi-meme.
        /// </summary>
        private static MonoBehaviour FindComponentByName(Scene scene, string typeName, GameObject host)
        {
            List<GameObject> roots = new List<GameObject>(scene.rootCount);
            scene.GetRootGameObjects(roots);

            for (int r = 0; r < roots.Count; r++)
            {
                MonoBehaviour[] all = roots[r].GetComponentsInChildren<MonoBehaviour>(true);

                for (int i = 0; i < all.Length; i++)
                {
                    MonoBehaviour mb = all[i];
                    if (mb == null) continue;
                    if (mb.gameObject == host) continue;
                    if (mb.GetType().Name != typeName) continue;

                    return mb;
                }
            }

            return null;
        }

        private static bool HasComponentNamed(GameObject target, string typeName)
        {
            MonoBehaviour[] all = target.GetComponents<MonoBehaviour>();

            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].GetType().Name == typeName) return true;

            return false;
        }
    }
}
