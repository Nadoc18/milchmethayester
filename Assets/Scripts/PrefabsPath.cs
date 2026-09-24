public static class PrefabsPath
{
    public static readonly int[] Plain = new int[] {13,14 };
    public static readonly int[] Desert = new int[] {11, 12 };
    public static readonly int[] Mountain = new int[] { 8, 9,10 };
    public static readonly int[] Base = new int[] {0, 1 };
    public static readonly int[] Portal = new int[] {18, 18 };
    public static readonly int[] Crystal = new int[] {  15, 16,17 };
    public static readonly int[] Gas = new int[] {  5, 6,7 };
    public static readonly int[] Hill = new int[] {  2, 3 ,4};
    public static readonly int[] Enemy = new int[] { 22, 23,24};
    public static readonly int[] Attacker = new int[] {  20, 21 };
    // La ruine d'un Shofar. Niveau 0 : l'epave (le meme modele que celui pose au
    // moment de la destruction). Niveau 1 : le Shofar RETOURNE - on reprend le modele
    // du Cristal au rang 3, la seule structure lumineuse du jeu, faute d'un modele
    // dedie. Change l'indice ici le jour ou tu en modelises un.
    public static readonly int[] Destroyed = new int[] { 39, 17 };
   
    public static int[] GetHexagonPrefabs(TypeOfHex state)
    {
        switch (state) {
        case TypeOfHex.desert: return Desert;
        case TypeOfHex.plain: return Plain;
        case TypeOfHex.mountain:   return Mountain;
        case TypeOfHex.Base: return Base;
        case TypeOfHex.portal: return Portal;
        case TypeOfHex.crystal:return Crystal;
        case TypeOfHex.gas: return Gas;
        case TypeOfHex.hill: return Hill;  
        case TypeOfHex.Destroyed: return Destroyed;
        case TypeOfHex.None: return null;
        }
        return null;
    }
    public static int[] GetPawnPrefabs(TypeOfPawn pawn)
    {
        switch (pawn)
        {
            case TypeOfPawn.enemy:    return Enemy;
            case TypeOfPawn.unit:
            case TypeOfPawn.scout:
            case TypeOfPawn.heavy:
            case TypeOfPawn.artillery:
                return Attacker;
            case TypeOfPawn.Bunker: return Hill;
            case TypeOfPawn.None:      return null;   
        
        }
        return null;
    }

    public static int[] GetDestroyedHexagonPrefabs(TypeOfHex state)
    {
        switch (state)
        {
            case TypeOfHex.desert: return new int [] {11,25,26};
            case TypeOfHex.plain: return new int[] { 13,13, 14 };
            case TypeOfHex.mountain: return new int[] { 8,33,34 };
            case TypeOfHex.Base: return new int[] { 29, 29,29 };
            case TypeOfHex.portal: return   new int[] {39, 39, 39 };
            case TypeOfHex.crystal: return new int[] { 27,27, 28 };
            case TypeOfHex.gas: return new int[] { 35,35, 36 };
            case TypeOfHex.hill: return new int[] { 2, 37, 38 };
            case TypeOfHex.None: return null;
        }
        return null;
    }

    public static int[] GetDestroyedtPawnPrefabs(TypeOfPawn pawn)
    {
        switch (pawn)
        {
            case TypeOfPawn.enemy: return new int[] { 30, 31,32 };
            case TypeOfPawn.unit:
            case TypeOfPawn.scout:
            case TypeOfPawn.heavy:
            case TypeOfPawn.artillery:
                return new int[] { 20, 25, 26 };
            case TypeOfPawn.Bunker: return new int[] { 2,37, 38 };
            case TypeOfPawn.None: return null;

        }
        return null;
    }
}

