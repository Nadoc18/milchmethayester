using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class PlayVideo : MonoBehaviour
{

    public VideoPlayer video;
    public bool playOnAwake = false;
    public bool loop = false;

    void Update()
    {
        if (playOnAwake)
        {
            Play();
            playOnAwake = false;
        }
    
    
    
    }


        public void Play()
    {
        video.Play();
    }
}
