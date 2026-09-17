using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VFXManager : MonoBehaviour
{
    public static VFXManager instance;


    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }

    }


  


    public void EvolutionFX(GameObject newAsset, GameObject assetToDestroy)
    {


        StartCoroutine(EvolutionFXCoroutine(newAsset, assetToDestroy));



    }


    private IEnumerator EvolutionFXCoroutine(GameObject newAsset, GameObject assetToDestroy)
    {

        for(int _i=0; _i< 2; _i++)
        {
            yield return new WaitForSeconds(0.02f);
            newAsset.SetActive(false);
            if(assetToDestroy != null)
            assetToDestroy.SetActive(true);
            yield return new WaitForSeconds(0.02f);
            newAsset.SetActive(true);
            if (assetToDestroy != null)
                assetToDestroy.SetActive(false);

        }
        if (assetToDestroy != null)
            Destroy(assetToDestroy);

    }


}
