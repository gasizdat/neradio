namespace Neradio.Core.Playback;

public enum StopKind
{
    User,
    // A phone call, an alarm or anything else that takes the audio for a while.
    Interruption,
    // Any other stop the user didn't make, such as another app taking over the audio.
    System,
}
