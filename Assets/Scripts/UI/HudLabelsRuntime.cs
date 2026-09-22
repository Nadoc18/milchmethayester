using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MNLTHII.UI
{
    /// <summary>
    /// Lecture d'un libelle de Assets/Resources/hud_labels.json PENDANT LE JEU.
    ///
    /// D'habitude c'est le HudBuilder qui lit ce fichier et recopie les textes dans
    /// l'inspecteur. Mais un libelle ajoute apres coup resterait vide tant que le HUD
    /// n'est pas reconstruit ; celui-ci le retrouve seul, au lancement.
    ///
    /// Recherche volontairement simple : la PREMIERE occurrence de "cle": "texte"
    /// dans le fichier, quelle que soit la section. Les cles lues ainsi doivent donc
    /// etre uniques dans le JSON (crystalName, menuTitle...).
    ///
    /// Le .cs reste en pur ASCII : l'hebreu ne vit que dans le JSON.
    ///
    /// Note d'optimisation : le fichier est charge une fois, et chaque cle est
    /// gardee en cache apres sa premiere lecture. Appele seulement au lancement.
    /// </summary>
    public static class HudLabelsRuntime
    {
        private static string _json;
        private static bool _loaded;
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>(16);

        public static string Get(string key)
        {
            return Get(key, "");
        }

        public static string Get(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;

            string cached;
            if (_cache.TryGetValue(key, out cached)) return string.IsNullOrEmpty(cached) ? fallback : cached;

            if (!_loaded)
            {
                _loaded = true;
                TextAsset asset = Resources.Load<TextAsset>("hud_labels");
                _json = (asset != null) ? asset.text : null;
                if (_json == null) Debug.LogWarning("[Libelles] Resources/hud_labels.json introuvable.");
            }

            string value = Find(_json, key);
            _cache[key] = value;
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string Find(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;

            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, System.StringComparison.Ordinal);
            if (at < 0) return null;

            int i = at + needle.Length;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != ':') return null;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;

            StringBuilder sb = new StringBuilder(64);
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"') return sb.ToString();

                if (c != '\\' || i >= json.Length)
                {
                    sb.Append(c);
                    continue;
                }

                char e = json[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': break;
                    case 'u':
                        if (i + 4 <= json.Length)
                        {
                            int code;
                            if (int.TryParse(json.Substring(i, 4), System.Globalization.NumberStyles.HexNumber,
                                             System.Globalization.CultureInfo.InvariantCulture, out code))
                                sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return null;
        }
    }
}
