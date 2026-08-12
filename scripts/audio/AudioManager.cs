using Godot;


public partial class AudioManager : Node
{
    const string MASTER_BUS = "Master";
    const string MUSIC_BUS = "Music";
    const string SFX_BUS = "SFX";


    public int music_audio_player_count = 2;
    public int sfx_audio_player_count = 6;
    public int current_music_player_index = 0;
    public AudioStreamPlayer[] music_audio_players = new AudioStreamPlayer[3];

    public AudioStreamPlayer[] sfx_audio_players = new AudioStreamPlayer[3];

    enum Bus
    {
        MASTER,
        MUSIC,
        SFX
    }

    public override void _Ready()
    {
        InitMusicAudioManager();
        InitSFXAudioManager();
    }

    public void InitMusicAudioManager()
    {
        music_audio_players = new AudioStreamPlayer[music_audio_player_count];

        for (int i = 0; i < music_audio_player_count; i++)
        {
            AudioStreamPlayer audioPlayer = new AudioStreamPlayer();
            audioPlayer.ProcessMode = Node.ProcessModeEnum.Always;
            audioPlayer.Bus = "Music";
            audioPlayer.Finished += () => OnMusicFinished(audioPlayer);
            AddChild(audioPlayer);
            music_audio_players[i] = audioPlayer;
        }

    }

    private void OnMusicFinished(AudioStreamPlayer audioPlayer)
    {
        if (
            audioPlayer == null ||
            audioPlayer.Stream == null ||
            music_audio_players[current_music_player_index] != audioPlayer
        )
        {
            return;
        }

        audioPlayer.Play();
    }

    public void PlayMusic(AudioStream audioStream)
    {
        AudioStreamPlayer currentMusicPlayer = music_audio_players[current_music_player_index];
        if(currentMusicPlayer.Stream == audioStream)
        {
            return;
        }
        int emptyAudioPlayerIndex = current_music_player_index == 1 ? 0 : 1;
        AudioStreamPlayer emptyAudioPlayer = music_audio_players[emptyAudioPlayerIndex];
        currentMusicPlayer.Stop();
        currentMusicPlayer.Stream = null;
        emptyAudioPlayer.Stream = audioStream;
        emptyAudioPlayer.Play();
        current_music_player_index = emptyAudioPlayerIndex;
    }

    public void InitSFXAudioManager()
    {
        sfx_audio_players = new AudioStreamPlayer[sfx_audio_player_count];

        for (int i = 0; i < sfx_audio_player_count; i++)
        {
            AudioStreamPlayer audioPlayer = new AudioStreamPlayer();
            audioPlayer.Bus = "SFX";
            AddChild(audioPlayer);
            sfx_audio_players[i] = audioPlayer;
        }

    }

    public void StopMusic()
    {
        foreach (AudioStreamPlayer audioPlayer in music_audio_players)
        {
            if(!GodotObject.IsInstanceValid(audioPlayer))
                continue;

            audioPlayer.Stop();
            audioPlayer.Stream = null;
        }
        current_music_player_index = 0;
    }

    public void PlaySFX(AudioStream audioStream)
    {
        if (audioStream == null || sfx_audio_players.Length == 0)
            return;

        AudioStreamPlayer audioPlayer = sfx_audio_players[0];
        for (int i = 1; i < sfx_audio_player_count; i++)
        {
            if (sfx_audio_players[i].Playing == false)
            {
                audioPlayer = sfx_audio_players[i];
                break;
            }
        }
        audioPlayer.Stream = audioStream;
        audioPlayer.Play();
    }


}
