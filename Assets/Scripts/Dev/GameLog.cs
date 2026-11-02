using UnityEngine;

// System.Diagnostics apporte un autre "Debug" que celui d'Unity : importer le
// namespace entier rendait chaque Debug.Log ambigu. On ne prend donc que
// l'attribut dont on a besoin, sous un alias.
using ConditionalAttribute = System.Diagnostics.ConditionalAttribute;

namespace MNLTHII.Dev
{
    /// <summary>
    /// LE JOURNAL DU JEU. Tous les Debug.Log de deroulement (tours, economie, tirs,
    /// deploiements) passent par ici, et plus jamais par Debug.Log directement.
    ///
    /// POURQUOI. Trois raisons, dans l'ordre d'importance :
    ///
    ///   1. UN LOG N'EST PAS GRATUIT. Unity capture une pile d'appel a chaque
    ///      Debug.Log : de l'ordre d'une fraction de milliseconde, et le jeu en ecrit
    ///      une vingtaine par tour. En audit a l'allure Eclair, cela se voit - le temps
    ///      d'image moyen passait de 6-7 ms sur les premieres parties a 14 ms sur les
    ///      dernieres, pendant que la console de l'editeur se remplissait de dizaines
    ///      de milliers de lignes. Ce n'etait pas une fuite du jeu : c'etait le journal.
    ///
    ///   2. EN WEBGL, C'EST PIRE. Chaque ligne traverse le pont vers la console du
    ///      navigateur, et la concatenation des chaines alloue au passage. Sur
    ///      telephone, c'est du temps et de la memoire pris au jeu.
    ///
    ///   3. [Conditional] EFFACE L'APPEL, PAS SEULEMENT SON EFFET. Dans un build de
    ///      production (ni editeur, ni build de developpement), le compilateur retire
    ///      l'appel ET le calcul de ses arguments : la chaine n'est jamais construite.
    ///      Un "if (verbose)" a l'interieur de la methode n'aurait pas evite ca.
    ///
    /// Les erreurs et les avertissements ne passent PAS par ici : ils restent des
    /// Debug.LogError / Debug.LogWarning, ils sont rares, et on veut les voir dans
    /// tous les builds.
    /// </summary>
    public static class GameLog
    {
        /// <summary>
        /// Decoche pendant un audit : le pilote joue des centaines de tours et n'a pas
        /// besoin du detail de chaque tir. Les notes de l'audit, elles, continuent de
        /// s'ecrire dans son rapport.
        /// </summary>
        public static bool Verbose = true;

        /// <summary>
        /// A appeler une fois au demarrage : Unity ne capture plus de pile d'appel pour
        /// les logs ordinaires. C'est l'essentiel du cout d'un log, et une pile d'appel
        /// sur "[Economie] +12 Energie" n'a jamais servi a personne. Les erreurs et les
        /// exceptions gardent la leur, evidemment.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void TrimStackTraces()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
        }

        [ConditionalAttribute("UNITY_EDITOR"), ConditionalAttribute("DEVELOPMENT_BUILD")]
        public static void Say(string message)
        {
            if (Verbose) Debug.Log(message);
        }

        [ConditionalAttribute("UNITY_EDITOR"), ConditionalAttribute("DEVELOPMENT_BUILD")]
        public static void Say(string format, object a)
        {
            if (Verbose) Debug.LogFormat(format, a);
        }

        [ConditionalAttribute("UNITY_EDITOR"), ConditionalAttribute("DEVELOPMENT_BUILD")]
        public static void Say(string format, object a, object b)
        {
            if (Verbose) Debug.LogFormat(format, a, b);
        }

        [ConditionalAttribute("UNITY_EDITOR"), ConditionalAttribute("DEVELOPMENT_BUILD")]
        public static void Say(string format, object a, object b, object c)
        {
            if (Verbose) Debug.LogFormat(format, a, b, c);
        }

        [ConditionalAttribute("UNITY_EDITOR"), ConditionalAttribute("DEVELOPMENT_BUILD")]
        public static void Say(string format, params object[] args)
        {
            if (Verbose) Debug.LogFormat(format, args);
        }
    }
}
