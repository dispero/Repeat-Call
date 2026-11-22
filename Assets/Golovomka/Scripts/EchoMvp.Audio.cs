using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        AudioSource phoneAudio, shotAudio;

        void InitializeAudio()
        {
            if (!roomCamera.TryGetComponent<AudioListener>(out _))
                roomCamera.gameObject.AddComponent<AudioListener>();

            phoneAudio = CreateAudioSource("PhoneRing", .65f);
            phoneAudio.loop = true;
            shotAudio = CreateAudioSource("ShotSound", .45f);
        }

        AudioSource CreateAudioSource(string clipName, float volume)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = volume;
            source.clip = Resources.Load<AudioClip>("Audio/" + clipName);
            return source;
        }

        void UpdatePhoneRing()
        {
            if (!phoneAudio || !phoneAudio.clip) return;

            bool shouldRing = zone == 0 && !phoneRead && mode == "play";
            if (shouldRing && !phoneAudio.isPlaying) phoneAudio.Play();
            else if (!shouldRing && phoneAudio.isPlaying) phoneAudio.Stop();
        }

        void PlayShot()
        {
            if (shotAudio && shotAudio.clip) shotAudio.PlayOneShot(shotAudio.clip);
        }
    }
}
