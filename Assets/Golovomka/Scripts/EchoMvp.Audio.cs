using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        AudioSource phoneAudio, shotAudio, menuSelectAudio, menuMusicAudio;
        AudioSource gameMusicAudio;
        AudioSource buttonCountdownAudio, buttonSignalAudio, correctButtonAudio;
        AudioSource targetRiseAudio, teleportAudio;
        bool buttonCountdownStarted;

        void InitializeAudio()
        {
            if (!roomCamera.TryGetComponent<AudioListener>(out _))
                roomCamera.gameObject.AddComponent<AudioListener>();

            phoneAudio = CreateAudioSource("PhoneRing", .65f);
            phoneAudio.loop = true;
            shotAudio = CreateAudioSource("ShotSound", .45f);
            menuSelectAudio = CreateAudioSource("MenuSelect", .65f);
            menuMusicAudio = CreateAudioSource("MenuMusic2", .3f);
            menuMusicAudio.loop = true;
            gameMusicAudio = CreateAudioSource("GameMusic", .3f);
            gameMusicAudio.loop = true;
            buttonCountdownAudio = CreateAudioSource("CountDown2", .65f);
            buttonSignalAudio = CreateAudioSource("Room3Sound", .65f);
            correctButtonAudio = CreateAudioSource("CorrectSound3", .65f);
            targetRiseAudio = CreateAudioSource("TargetSound", .65f);
            teleportAudio = CreateAudioSource("Teleportation", .65f);
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

        void PlayMenuSelect()
        {
            if (menuSelectAudio.clip) menuSelectAudio.Play();
        }

        void UpdateButtonCountdown()
        {
            var clip = buttonCountdownAudio.clip;
            if (buttonCountdownStarted || !clip || currentButtons.Solved) return;

            float timeLeft = currentButtons.startDelay - elapsed;
            if (timeLeft <= 0 || timeLeft > clip.length) return;

            buttonCountdownStarted = true;
            buttonCountdownAudio.Play();
        }

        void PlayButtonSignal()
        {
            if (buttonCountdownAudio.isPlaying) buttonCountdownAudio.Stop();
            if (buttonSignalAudio.clip) buttonSignalAudio.Play();
        }

        void PlayCorrectButton()
        {
            if (correctButtonAudio.clip) correctButtonAudio.Play();
        }

        void PlayTargetRise()
        {
            if (targetRiseAudio.clip) targetRiseAudio.Play();
        }
    }
}
