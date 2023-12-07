using Speech.Speaking;
using UnityEngine;

public class VoiceVoxSample : MonoBehaviour
{
    [SerializeField]
    private AudioSource audioSource;

    private async void Start()
    {
        VoiceVoxAPI voiceVoxAPI = new VoiceVoxAPI(VoiceVoxAPI.LOCAL_URL);
        string query = await voiceVoxAPI.GetAudioQuery(1, "こんにちは");
        AudioClip clip = await voiceVoxAPI.Synthesis(1, query);

        this.audioSource.PlayOneShot(clip);
    }
}
