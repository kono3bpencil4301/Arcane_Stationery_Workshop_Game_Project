using Godot;



public partial class Main : Node2D
{
	[Export]
	public AudioStream[] Music = new AudioStream[0];

	[Export]
	public AudioStream[] SFX = new AudioStream[0];
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (Music.Length == 0)
		{
			GD.PushWarning("Main 没有配置背景音乐。");
			return;
		}

		GetNode<AudioManager>("/root/AudioManager").PlayMusic(Music[1]);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
