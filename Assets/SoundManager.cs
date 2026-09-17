using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;
    private AudioSource m_AudioSource;
    [SerializeField] AudioClip m_backgrndAudio;
    bool fadeIn, fadeOut;
    [SerializeField] float minPitch, maxPitch;

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

    // Start is called before the first frame update
    void Start()
    {
        m_AudioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {

        if (fadeIn)
        {
            m_AudioSource.pitch += 0.1f;
            if (m_AudioSource.pitch > maxPitch)
            {
                m_AudioSource.Play();
                fadeIn = false;
            }

        }
        if (fadeOut)
        {
            m_AudioSource.pitch -= 0.1f;
            if (m_AudioSource.pitch < minPitch)
            {
                m_AudioSource.Stop();
                fadeOut = false;
            }

        }

    }


    public void FadeOut()
    {
        fadeOut = true;
    }
    public void FadeIn()
    {
        fadeIn = true;
    }



}
