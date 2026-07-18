using System;

using SonicLair.Lib.Services;
using SonicLair.Lib.Types;

using Windows.Media;
using Windows.Storage.Streams;

namespace SonicLairXbox.Services
{
    /// <summary>
    /// Wires the app's playback (driven by IMusicPlayerService/LibVLC) to the
    /// System Media Transport Controls (SMTC). This is what tells Windows/Xbox
    /// "real background audio is playing" so the console doesn't suspend the
    /// process the moment the user launches a game or another app, and it's
    /// what makes Play/Pause/Next/Previous show up in the Xbox guide.
    ///
    /// Because this touches Windows.Media (a UWP-only namespace) it lives in
    /// the Xbox project, NOT in SonicLair.Lib -- the CLI project targets
    /// plain .NET and would fail to build if this were added there.
    /// </summary>
    public class BackgroundAudioService
    {
        private readonly IMusicPlayerService _player;
        private readonly ISubsonicService _client;
        private readonly SystemMediaTransportControls _smtc;

        public BackgroundAudioService(IMusicPlayerService player, ISubsonicService client)
        {
            _player = player;
            _client = client;

            _smtc = SystemMediaTransportControls.GetForCurrentView();
            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;
            _smtc.ButtonPressed += Smtc_ButtonPressed;

            _player.RegisterCurrentStateHandler(OnCurrentStateChanged);

            // In case a track is already loaded/playing when this is constructed.
            UpdateFromState(_player.GetCurrentState());
        }

        private void Smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _player.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    _player.Pause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    _player.Next();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    _player.Prev();
                    break;
            }
        }

        private void OnCurrentStateChanged(object sender, CurrentStateChangedEventArgs e)
        {
            UpdateFromState(e.CurrentState);
        }

        private void UpdateFromState(CurrentState state)
        {
            _smtc.PlaybackStatus = state.IsPlaying
                ? MediaPlaybackStatus.Playing
                : MediaPlaybackStatus.Paused;

            var track = state.CurrentTrack;
            if (track == null || string.IsNullOrEmpty(track.Id))
            {
                return;
            }

            var updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = track.Title ?? "";
            updater.MusicProperties.Artist = track.Artist ?? "";
            updater.MusicProperties.AlbumTitle = track.Album ?? "";

            try
            {
                var coverArtUri = _client.GetCoverArtUri(track.AlbumId);
                if (!string.IsNullOrEmpty(coverArtUri))
                {
                    updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(coverArtUri));
                }
            }
            catch (Exception)
            {
                // Thumbnail is a nice-to-have; a bad/unreachable cover art URL
                // shouldn't take down the transport controls.
            }

            updater.Update();
        }
    }
}
