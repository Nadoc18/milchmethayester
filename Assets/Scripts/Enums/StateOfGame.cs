using System;

[Serializable]
public enum StateOfGame
{   inGame,
    startOfTurn,
    endOfTurn,
    Interactions,
    victory,
    defeat,
    waitingForChallenge,
    none
}
