using System.Text;

namespace MNLTHII.UI
{
    /// <summary>
    /// CHIFFRES ET MOTS LATINS DANS UN TEXTE HEBREU.
    ///
    /// TextMeshPro en mode "Right to Left" ne fait pas de bidi : il inverse l'ordre
    /// de TOUS les caracteres. L'hebreu, ecrit a l'envers dans la chaine, ressort
    /// a l'endroit - mais "40" devient "04", "3/4" devient "4/3", et "Space" devient
    /// "ecapS". Jusqu'ici chaque champ de chiffres etait un TextMeshPro separe, non
    /// RTL ; des qu'une phrase hebraique porte un nombre, il faut passer par ici.
    ///
    /// Fix() pre-inverse chaque suite de chiffres ou de lettres latines (avec les
    /// separateurs qu'on trouve a l'interieur d'un nombre : / . : % + -), de sorte
    /// que l'inversion globale de TMP la remette a l'endroit. Tout le reste est
    /// rendu tel quel. A appliquer UNIQUEMENT sur un champ dont isRightToLeftText
    /// est vrai : sur un champ normal, le texte sortirait a l'envers.
    ///
    /// Note d'optimisation : un seul StringBuilder partage, aucune allocation si
    /// la chaine ne contient ni chiffre ni lettre latine (on rend la chaine d'origine).
    /// </summary>
    public static class Rtl
    {
        private static readonly StringBuilder _builder = new StringBuilder(256);

        public static string Fix(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            bool any = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (IsLatin(text[i])) { any = true; break; }
            }
            if (!any) return text;

            _builder.Length = 0;

            int n = text.Length;
            int i2 = 0;
            while (i2 < n)
            {
                char c = text[i2];
                if (!IsLatin(c))
                {
                    _builder.Append(c);
                    i2++;
                    continue;
                }

                // Une suite latine : lettres, chiffres, et les separateurs internes
                // (un separateur ne fait partie de la suite que s'il est suivi d'un
                // autre caractere latin : "4 (point median)" garde son separateur a sa place).
                int start = i2;
                int end = i2;
                while (end < n)
                {
                    char e = text[end];
                    if (IsLatin(e)) { end++; continue; }
                    if (IsInnerSeparator(e) && end + 1 < n && IsLatin(text[end + 1])) { end++; continue; }
                    break;
                }

                for (int k = end - 1; k >= start; k--) _builder.Append(text[k]);
                i2 = end;
            }

            return _builder.ToString();
        }

        private static bool IsLatin(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static bool IsInnerSeparator(char c)
        {
            return c == '/' || c == '.' || c == ':' || c == '%' || c == '+' || c == '-' || c == ',' || c == ' ';
        }
    }
}
