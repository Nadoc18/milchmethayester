using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MNLTHII.UI
{
    /// <summary>
    /// FONT FEATURES SUR "NOTHING", MAIS AU LANCEMENT.
    ///
    /// TextMeshPro active le crenage par defaut. Sur du latin c'est souhaitable ; sur
    /// de l'hebreu en RTL ca ne l'est pas : les paires de crenage des polices
    /// hebraiques sont pensees pour un flux gauche-droite, et TMP les applique APRES
    /// avoir inverse l'ordre des glyphes. Les decalages tombent du mauvais cote de la
    /// lettre, les mots se resserrent ou se decollent au hasard - c'est illisible.
    ///
    /// Le HudBuilder decoche deja cette case sur tous les textes qu'il construit, mais
    /// il travaille dans l'editeur, avec SerializedObject. Les textes crees PENDANT LE
    /// JEU (bandeaux d'etape, pastilles de role, confirmations) n'y passent pas.
    ///
    /// Ici on fait la meme chose par reflexion : le nom du champ a change entre les
    /// versions de TMP (m_enableKerning avant, une liste m_ActiveFontFeatures ensuite).
    /// En cherchant les deux, le code fonctionne quelle que soit la version installee,
    /// et ne casse rien s'il ne trouve ni l'un ni l'autre.
    ///
    /// Note d'optimisation : la reflexion est faite UNE fois (les champs sont retenus
    /// dans des statiques), puis chaque appel n'est qu'une affectation.
    /// </summary>
    public static class TextFeatures
    {
        private static bool _searched;
        private static FieldInfo _activeFeatures;   // List<OTL_FeatureTag>
        private static FieldInfo _enableKerning;    // bool (anciennes versions)
        private static PropertyInfo _kerningProperty;

        public static void Disable(TMPro.TMP_Text text)
        {
            if (text == null) return;

            Search(text.GetType());

            if (_activeFeatures != null)
            {
                IList list = _activeFeatures.GetValue(text) as IList;
                if (list != null && list.Count > 0) list.Clear();
            }

            if (_enableKerning != null) _enableKerning.SetValue(text, false);
            else if (_kerningProperty != null && _kerningProperty.CanWrite) _kerningProperty.SetValue(text, false, null);

            text.SetAllDirty();
        }

        private static void Search(System.Type type)
        {
            if (_searched) return;
            _searched = true;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            System.Type walk = type;
            while (walk != null && walk != typeof(object))
            {
                if (_activeFeatures == null) _activeFeatures = walk.GetField("m_ActiveFontFeatures", Flags);
                if (_enableKerning == null) _enableKerning = walk.GetField("m_enableKerning", Flags);
                if (_kerningProperty == null) _kerningProperty = walk.GetProperty("enableKerning", Flags);

                walk = walk.BaseType;
            }

            if (_activeFeatures == null && _enableKerning == null && _kerningProperty == null)
                Debug.LogWarning("[Textes] Impossible de desactiver le crenage de TMP par reflexion : "
                               + "les textes crees pendant le jeu garderont le crenage.");
        }
    }
}
