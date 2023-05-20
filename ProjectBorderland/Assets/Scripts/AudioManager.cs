using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // Singleton instance variable
    private static AudioManager instance;

    [Header("Audio Sources")]
    [SerializeField] AudioSource SFXSource;
    [SerializeField] AudioSource musicSource;

    [Header("Audio Clips")]
    public AudioClip backgroundMusic;

    public AudioClip phaseShiftSFX;
    public AudioClip walkingSFX;
    public AudioClip dashSFX;
    public AudioClip deathSFX;
    public AudioClip jumpSFX;
    public AudioClip climbingSFX;
    public AudioClip checkpointSFX;
    public AudioClip clickSFX;

    // Getter for the singleton instance
    public static AudioManager Instance
    {
        get { return instance; }
    }

    private void Awake()
    {
        // Check if an instance already exists
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        musicSource.clip = backgroundMusic;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        SFXSource.PlayOneShot(clip);
    }
}