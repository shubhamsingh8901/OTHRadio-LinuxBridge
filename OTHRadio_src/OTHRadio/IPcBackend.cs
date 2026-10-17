namespace OTHRadio;

internal interface IPcBackend
{
	string Title { get; }

	string Artist { get; }

	string App { get; }

	bool HasSession { get; }

	bool IsPlaying { get; }

	bool Available { get; }

	bool Fast { get; set; }

	string Error { get; }

	string UnavailableHint { get; }

	string BridgeFolder { get; }

	float Volume { get; }

	void Start();

	void Stop();

	void Send(PcCommand command);

	void SetVolume(float volume);

	(double pos, double len) Progress();
}
