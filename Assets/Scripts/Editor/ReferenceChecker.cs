using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MNLTHII.EditorTools
{
    /// <summary>
    /// Passe la scene au peigne fin et dit QUELLE case de l'inspecteur est vide.
    ///
    /// A quoi ca sert : une NullReferenceException du genre
    ///     GameCanvasController.RefreshHexClickedView (at ...:244)
    /// ne nomme jamais le champ fautif. Elle dit "quelque chose est nul", et il faut
    /// deviner lequel des quarante champs du composant. Cet outil fait l'inverse :
    /// il liste les champs, un par un, avec leur nom exact.
    ///
    /// Il signale quatre choses, par ordre de gravite :
    ///
    ///   COMPOSANT VIDE - TOUTES les references d'un composant sont tombees d'un
    ///                    coup. Ce n'est jamais une erreur de saisie : c'est la
    ///                    scene chargee en memoire qui a perdu ses donnees, le plus
    ///                    souvent apres une erreur de compilation. LE FICHIER SUR LE
    ///                    DISQUE EST PROBABLEMENT ENCORE BON : il ne faut surtout
    ///                    pas enregistrer, mais recharger la scene sans sauver.
    ///                    Le menu juste en dessous le fait proprement.
    ///
    ///   DOUBLON        - deux fois le meme composant sur le meme objet. Le premier
    ///                    Awake gagne le singleton, et si c'est le doublon vide qui
    ///                    passe en tete, c'est lui qui recoit les evenements.
    ///
    ///   CASSEE         - le champ pointait vers un objet SUPPRIME (le "Missing
    ///                    (Type)" de l'inspecteur). Un objet a disparu de la scene.
    ///
    ///   VIDE           - une case n'a jamais rien recu. Souvent normal, donc
    ///                    signale seulement pour les composants de la liste Watched.
    ///
    /// Menu : Milchemet > Verifier les references de la scene
    ///
    /// Aucun risque : l'outil ne modifie rien, il lit et il journalise. Chaque ligne
    /// du log est cliquable et selectionne l'objet concerne.
    /// </summary>
    public static class ReferenceChecker
    {
        /// <summary>
        /// Composants dont on veut AUSSI connaitre les cases simplement vides.
        /// Comparaison sur le nom du type, pour ne rien exiger a la compilation :
        /// l'outil continue de fonctionner meme si l'un d'eux est renomme ou retire.
        /// </summary>
        private static readonly string[] Watched =
        {
            "GameCanvasController",
            "HudController",
            "HexTooltipController",
            "PortalPanelController",
            "TriviaPanelController",
            "UnitActionCard",
            "TriviaManager",
            "ThreatPreview"
        };

        /// <summary>
        /// En dessous de ce nombre de references, "toutes vides" ne veut rien dire :
        /// un composant a deux champs peut legitimement n'en avoir aucun de rempli.
        /// </summary>
        private const int WipeSuspicionThreshold = 4;

        [MenuItem("Milchemet/Verifier les references de la scene", false, 30)]
        public static void Check()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Verif] Aucune scene ouverte.");
                return;
            }

            List<GameObject> roots = new List<GameObject>(scene.rootCount);
            scene.GetRootGameObjects(roots);

            int broken = 0;
            int empty = 0;
            int wiped = 0;
            int duplicates = 0;
            int missingScripts = 0;
            int componentsSeen = 0;

            StringBuilder line = new StringBuilder(200);

            // Reutilises d'un objet a l'autre : aucune allocation dans la boucle.
            List<string> seenTypes = new List<string>(32);
            List<MonoBehaviour> behaviourBuffer = new List<MonoBehaviour>(32);

            for (int r = 0; r < roots.Count; r++)
            {
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);

                for (int t = 0; t < all.Length; t++)
                {
                    Transform node = all[t];

                    behaviourBuffer.Clear();
                    node.GetComponents(behaviourBuffer);

                    seenTypes.Clear();

                    for (int b = 0; b < behaviourBuffer.Count; b++)
                    {
                        MonoBehaviour mb = behaviourBuffer[b];

                        if (mb == null)
                        {
                            missingScripts++;

                            line.Length = 0;
                            line.Append("[Verif] SCRIPT MANQUANT  ");
                            line.Append(Path(node));
                            Debug.LogError(line.ToString(), node.gameObject);
                            continue;
                        }

                        componentsSeen++;

                        string typeName = mb.GetType().Name;

                        // --- doublon sur le meme objet ---
                        if (seenTypes.Contains(typeName))
                        {
                            duplicates++;

                            line.Length = 0;
                            line.Append("[Verif] DOUBLON  ");
                            line.Append(Path(node));
                            line.Append(" porte DEUX fois ");
                            line.Append(typeName);
                            line.Append("  (le premier Awake gagne le singleton : si c'est le doublon vide, c'est lui qui recevra les evenements)");

                            Debug.LogError(line.ToString(), mb);
                        }
                        else
                        {
                            seenTypes.Add(typeName);
                        }

                        Inspect(mb, typeName, line, ref broken, ref empty, ref wiped);
                    }
                }
            }

            Debug.LogFormat("[Verif] Termine. {0} composants inspectes : {1} composant(s) entierement vide(s), "
                          + "{2} doublon(s), {3} reference(s) cassee(s), {4} case(s) vide(s) sur les composants "
                          + "surveilles, {5} script(s) manquant(s).",
                            componentsSeen, wiped, duplicates, broken, empty, missingScripts);

            if (wiped > 0)
            {
                Debug.LogError("[Verif] UN COMPOSANT ENTIEREMENT VIDE N'EST PAS UNE ERREUR DE SAISIE. "
                             + "La scene chargee en memoire a perdu ses donnees, mais le fichier sur le "
                             + "disque est probablement encore bon. N'ENREGISTRE PAS : utilise "
                             + "Milchemet > Recharger la scene depuis le disque.");
            }

            if (broken == 0 && empty == 0 && wiped == 0 && duplicates == 0 && missingScripts == 0)
                Debug.Log("[Verif] Rien a signaler : toutes les references de la scene tiennent.");
        }

        /// <summary>Parcourt les champs d'un composant et compte ce qui manque.</summary>
        private static void Inspect(MonoBehaviour mb, string typeName, StringBuilder line,
                                    ref int broken, ref int empty, ref int wiped)
        {
            bool watched = IsWatched(typeName);

            SerializedObject so = new SerializedObject(mb);
            SerializedProperty prop = so.GetIterator();

            int refFields = 0;
            int filledFields = 0;
            int brokenHere = 0;

            // Premier passage : compter, sans rien journaliser. On ne peut pas savoir
            // si un champ vide est anodin avant de connaitre l'etat de tous les autres.
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.name == "m_Script") continue;

                refFields++;

                if (prop.objectReferenceValue != null) filledFields++;
                else if (prop.objectReferenceInstanceIDValue != 0) brokenHere++;
            }

            // --- composant entierement vide : le cas grave ---
            if (refFields >= WipeSuspicionThreshold && filledFields == 0 && brokenHere == 0)
            {
                wiped++;

                line.Length = 0;
                line.Append("[Verif] COMPOSANT VIDE  ");
                line.Append(Path(mb.transform));
                line.Append(" > ");
                line.Append(typeName);
                line.Append("  : ses ");
                line.Append(refFields);
                line.Append(" references sont TOUTES vides. Ce composant a perdu ses donnees.");

                Debug.LogError(line.ToString(), mb);
                return;                       // inutile de lister les 40 champs un par un
            }

            broken += brokenHere;

            if (brokenHere == 0 && !watched) return;

            // Second passage : maintenant on nomme.
            so = new SerializedObject(mb);
            prop = so.GetIterator();
            enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.name == "m_Script") continue;
                if (prop.objectReferenceValue != null) continue;

                bool isBroken = prop.objectReferenceInstanceIDValue != 0;
                if (!isBroken && !watched) continue;

                line.Length = 0;
                line.Append(isBroken ? "[Verif] CASSEE  " : "[Verif] VIDE    ");
                line.Append(Path(mb.transform));
                line.Append(" > ");
                line.Append(typeName);
                line.Append(" . ");
                line.Append(prop.propertyPath);
                if (isBroken) line.Append("  (l'objet vise a ete supprime)");

                if (isBroken) Debug.LogError(line.ToString(), mb);
                else { empty++; Debug.LogWarning(line.ToString(), mb); }
            }
        }

        // =================================================================
        //  RECHARGEMENT
        // =================================================================
        /// <summary>
        /// Recharge la scene active depuis le disque, en jetant ce qui est en memoire.
        ///
        /// C'est la manoeuvre de secours quand un composant s'est vide : le fichier
        /// .unity est presque toujours encore intact, et le reflexe naturel - Ctrl+S -
        /// est precisement celui qui le detruit. Ce menu fait l'inverse, et demande
        /// confirmation parce que l'operation est irreversible dans l'autre sens.
        /// </summary>
        [MenuItem("Milchemet/Recharger la scene depuis le disque", false, 31)]
        public static void ReloadScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();

            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[Verif] La scene active n'a pas de fichier sur le disque : rien a recharger.");
                return;
            }

            bool go = EditorUtility.DisplayDialog(
                "Recharger la scene",
                "La scene va etre rechargee depuis :\n\n" + scene.path
                + "\n\nTout ce qui n'a pas ete enregistre sera PERDU, y compris le HUD "
                + "genere (il se reconstruit en un clic).\n\nSi Unity redemande ensuite "
                + "d'enregistrer, reponds NON : c'est justement ce qu'on veut jeter.\n\nContinuer ?",
                "Recharger", "Annuler");

            if (!go) return;

            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            Debug.LogFormat("[Verif] Scene rechargee depuis {0}.", scene.path);
        }

        // =================================================================
        //  OUTILS
        // =================================================================
        private static bool IsWatched(string typeName)
        {
            for (int i = 0; i < Watched.Length; i++)
                if (Watched[i] == typeName) return true;
            return false;
        }

        /// <summary>Chemin lisible dans la hierarchie, pour retrouver l'objet d'un coup d'oeil.</summary>
        private static string Path(Transform t)
        {
            StringBuilder sb = new StringBuilder(64);
            sb.Append(t.name);

            Transform parent = t.parent;
            int guard = 0;
            while (parent != null && guard < 32)
            {
                sb.Insert(0, " / ");
                sb.Insert(0, parent.name);
                parent = parent.parent;
                guard++;
            }

            return sb.ToString();
        }
    }
}
