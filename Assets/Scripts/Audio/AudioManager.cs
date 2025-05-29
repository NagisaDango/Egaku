using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class AudioManager : MonoBehaviour, IOnEventCallback
{

    public static AudioManager Instance;

    public static byte PlayAudioEventCode = 1;

    public const string GAMEBGM = "GameBgm";
    public const string MENUBGM = "MenuBgm";
    public const string CLICKSFX = "ClickSfx";

    public const string JUMPSFX = "JumpSfx";
    public const string DRAWSFX = "DrawSfx";
    public const string CLOUDBOUNCESFX = "CloudBounceSfx"; 
    public const string GLASSBREAKSFX = "GlassBreakSfx";
    public const string ERASESFX = "EraseSfx";
    public const string COLLECTSFX = "CollectSfx";

    [Header("General Music")]
    public AudioClip playBgm;
    public AudioClip menuBgm;

    [Header("Genral SFX")]
    public AudioClip clickSFX;

    [Header("Runner Sound Effects")]
    public AudioClip jumpSFX;
    public AudioClip collectSFX;

    [Header("Drawer Sound Effects")]
    public AudioClip drawSFX;
    public AudioClip cloudBounceSFX;
    public AudioClip glassBreakSFX;
    public AudioClip eraseSFX;

    public static PhotonView m_photonView;
    private const int AUDIO_CHANNEL_LIMIT = 10;
    private struct Channel
    {
        public AudioSource channel;
        public float lastPlayed;
    }

    private static Channel[] channels;
    private static Channel BGMChannel;

    public static Dictionary<string, AudioClip> NameAudioPair;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {

            Destroy(this.gameObject);
            return;
        }
        Instance = this;


        channels = new Channel[AUDIO_CHANNEL_LIMIT];
        for (int i = 0; i < channels.Length; i++)
        {
            channels[i].channel = this.gameObject.AddComponent<AudioSource>();
            channels[i].lastPlayed = 0;
        }
        BGMChannel.channel = this.gameObject.AddComponent<AudioSource>();
        BGMChannel.lastPlayed = 0;
        BGMChannel.channel.loop = true;
        BGMChannel.channel.volume = 0.1f;

        m_photonView = GetComponent<PhotonView>();
        InitAudioDict();
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        PlayBGM(MENUBGM);
    }

    [PunRPC]
    public void RPC_PlayOne(string name, bool randomPitch)
    {
        PlayOne(name, randomPitch);
    }

    private void InitAudioDict()
    {
        NameAudioPair = new Dictionary<string, AudioClip>();

        NameAudioPair[GAMEBGM] = playBgm;
        NameAudioPair[MENUBGM] = menuBgm;

        NameAudioPair[CLICKSFX] = clickSFX;

        // Runner Sound Effects
        NameAudioPair[JUMPSFX] = jumpSFX;
        NameAudioPair[COLLECTSFX] = collectSFX;

        // Drawer Sound Effects
        NameAudioPair[DRAWSFX] = drawSFX;
        NameAudioPair[CLOUDBOUNCESFX] = cloudBounceSFX;
        NameAudioPair[GLASSBREAKSFX] = glassBreakSFX;
        NameAudioPair[ERASESFX] = eraseSFX;
    }

    public static void PlayBGM(string name)
    {
        BGMChannel.channel.clip = NameAudioPair[name];
        BGMChannel.channel.Play();  
    }
    public static int PlayOne(string name, bool randomPitch = false)
    {
        for (int i = 0; i < channels.Length; ++i)
        {
            if (channels[i].channel.isPlaying &&
                channels[i].channel.clip == NameAudioPair[name] &&
                channels[i].lastPlayed >= Time.time - 0.2f)
            {
                channels[i].channel.Play();
                return -1;
            }
        }

        int oldest = -1;
        float time = 10000f;
        for(int i = 0; i < channels.Length; i++)
        {
            if(channels[i].channel.isPlaying &&
                channels[i].lastPlayed < time)
            {
                oldest = i;
                time = channels[i].lastPlayed;
            }
            if (!channels[i].channel.isPlaying)
            {
                channels[i].channel.clip = NameAudioPair[name];
                if(randomPitch)
                {
                    Debug.LogWarning("Random pitch not set yet");
                }
                channels[i].channel.Play();
                channels[i].lastPlayed = Time.time;
                return i;
            }
        }

        if (oldest > 0)
        {
            channels[oldest].channel.clip = NameAudioPair[name];
            channels[oldest].lastPlayed = Time.time;
            channels[oldest].channel.Play();
            Debug.Log("Replace channel due to over crowd");
            return oldest;
        }
        return -1;
    }

    public void PlayLoop(string name)
    {
        BGMChannel.channel.clip = NameAudioPair[name];
        BGMChannel.channel.loop = true;
        BGMChannel.channel.Play();
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;

        if (eventCode == PlayAudioEventCode)
        {
            object[] data = (object[])photonEvent.CustomData;

            PlayOne((string)data[0], (bool)data[1]);
        }
    }
}
