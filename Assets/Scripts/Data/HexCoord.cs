using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexCoord 
{

    public int q = 0;
    public int r = 0;
    public int s = 0;
    public int i = 0;
    public HexCoord(int q, int r, int s)
    {
        this.q = q; 
        this.r = r;
        this.s = s;
    }
    public bool CompareHexCoord(HexCoord coord2)
    {

        if (coord2 != null)
        {
            if (this.q == coord2.q && this.r == coord2.r && this.s == coord2.s)
                return true;
        }
        return false;
    }



}
