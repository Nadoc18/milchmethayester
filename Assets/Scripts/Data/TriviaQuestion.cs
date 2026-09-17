using System;
using System.Collections.Generic;

/// <summary>Les quatre domaines de questions du GDD V3 - Section 2.</summary>
[Serializable]
public enum TriviaCategory
{
    Halacha,
    Tanakh,
    Musar,
    Tefillah
}

[Serializable]
public class TriviaQuestion
{
    public string category;
    public string question;
    public string[] answers;
    public int correctIndex;

    /// <summary>
    /// 1 (facile) a 4 (difficile). C'est ce chiffre, et lui seul, qui fixe le gain
    /// annonce sur l'ecran de choix du sujet - voir InteractionRules.GetTriviaReward.
    ///
    /// L'ecran de choix montre donc le gain de la VRAIE question qui sera posee, pas
    /// un nombre decoratif tire au hasard. C'est ce qui rend le pari honnete : quand
    /// le joueur voit +85, il sait qu'il prend la question la plus dure de ce tour.
    ///
    /// Absent du JSON, le champ vaut 0 et IsValid le ramene a 1 : une banque de
    /// questions ecrite avant ce systeme continue de fonctionner.
    /// </summary>
    public int difficulty = 1;

    public bool IsValid()
    {
        if (difficulty < 1) difficulty = 1;
        if (difficulty > 4) difficulty = 4;

        return !string.IsNullOrEmpty(question)
               && answers != null
               && answers.Length >= 2
               && correctIndex >= 0
               && correctIndex < answers.Length;
    }
}

[Serializable]
public class TriviaQuestionBank
{
    public List<TriviaQuestion> questions = new List<TriviaQuestion>();
}
