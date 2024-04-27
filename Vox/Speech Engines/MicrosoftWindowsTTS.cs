﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

namespace Vox.Speech_Engines
{
    internal class MicrosoftWindowsTTS : PropertyChangedBase, ISpeechGenerator
    {
        #region Properties
        private string? _VoiceName;
        public string VoiceName
        {
            get { return GetField(ref _VoiceName); }
            set { SetField(ref _VoiceName, value); }
        }

        private int _VoiceSpeed;

        public int VoiceSpeed //As a percentage where 100 = 1.0x
        {
            get { return GetField(ref _VoiceSpeed); }
            set { SetField(ref _VoiceSpeed, value); }
        }

        private string? _ApiAddress;

        public string ApiAddress
        {
            get { return GetField(ref _ApiAddress); }
            set { SetField(ref _ApiAddress, value); }
        }

        private string? _ApiKey;

        public string ApiKey
        {
            get { return GetField(ref _ApiKey); }
            set { SetField(ref _ApiKey, value); }
        }

        private string? _ApiToken;

        public string ApiToken
        {
            get { return GetField(ref _ApiToken); }
            set { SetField(ref _ApiToken, value); }
        }

        private GeneratorState _State = GeneratorState.Uninitialized;

        public GeneratorState State
        {
            get { return GetField(ref _State); }
            private set { SetField(ref _State, value); }
        }


        private IReadOnlyCollection<InstalledVoice> _WindowsVoices = new List<InstalledVoice>();

        public IReadOnlyCollection<InstalledVoice> WindowsVoices
        {
            get { return _WindowsVoices; }
            set { SetField(ref _WindowsVoices, value); }
        }

        private InstalledVoice? _SelectedWindowsVoice;

        public InstalledVoice? SelectedWindowsVoice
        {
            get { return _SelectedWindowsVoice; }
            set { SetField(ref _SelectedWindowsVoice, value); }
        }

        private SpeechSynthesizer _Synth = new SpeechSynthesizer();

        public event EventHandler<SpeechStateUpdatedEventArgs>? SpeechStateUpdated;

        #endregion
        #region Implement ISpeechGenerator
        public void Initialize()
        {
            WindowsVoices = _Synth.GetInstalledVoices();
            _Synth.SpeakStarted += _Synth_SpeakStarted;
            _Synth.SpeakCompleted += _Synth_SpeakCompleted;
            State = GeneratorState.Ready;
        }

        public void Cleanup()
        {
            _Synth.Dispose();
        }

        public void Start(string text, string culture = "en-US")
        {
            if (_Synth.State != SynthesizerState.Ready)
            {
                _Synth.SpeakAsyncCancelAll();
                while (_Synth.State != SynthesizerState.Ready)
                    Thread.Sleep(50);
            }

            _Synth.SetOutputToDefaultAudioDevice();

            if (SelectedWindowsVoice != null)
            {
                _Synth.SelectVoice(SelectedWindowsVoice.VoiceInfo.Name);
            }

            var builder = new PromptBuilder();
            builder.StartVoice(new CultureInfo(culture));
            builder.AppendText(text);
            builder.EndVoice();
            _Synth.SpeakAsync(builder);
        }

        public void PauseUnpause()
        {
            switch (_Synth.State)
            {
                case SynthesizerState.Speaking:
                    _Synth.Pause();
                    UpdateState(GeneratorState.Paused);
                    //PauseResumeText = "Resume";
                    break;
                case SynthesizerState.Paused:
                    _Synth.Resume();
                    UpdateState(GeneratorState.Playing);
                    //PauseResumeText = "Pause";
                    break;
                case SynthesizerState.Ready:
                default:
                    break;
            }
        }

        public void Stop()
        {
            if (State == GeneratorState.Playing || State == GeneratorState.Paused)
                _Synth.SpeakAsyncCancelAll();

            UpdateState(GeneratorState.Ready);
        }

        public void OnSpeechStateChanged(GeneratorState state)
        {
            this.SpeechStateUpdated?.Invoke(this, new SpeechStateUpdatedEventArgs(state));
        }

        public void SaveSettings(StreamWriter file)
        {
            file.WriteLine(SelectedWindowsVoice?.VoiceInfo.Name); //Voice
            file.WriteLine(VoiceSpeed); //Rate
        }

        public void LoadSettings(StreamReader file)
        {
            if (file != null)
            {
                string? voiceName = file.ReadLine();
                string? speechRate = file.ReadLine();

                if (voiceName != null)
                {
                    SelectedWindowsVoice = WindowsVoices.FirstOrDefault(v => v.VoiceInfo.Name == voiceName);
                    if (SelectedWindowsVoice == null)
                        SelectedWindowsVoice = WindowsVoices.FirstOrDefault();
                }

                if (speechRate != null)
                {
                    if (int.TryParse(speechRate, out int intRate))
                    {
                        VoiceSpeed = intRate;
                    }
                }
            }
            else
            {
                SelectedWindowsVoice = WindowsVoices.FirstOrDefault();
            }
        }
        #endregion
        #region Internal Methods
        private void _Synth_SpeakCompleted(object? sender, SpeakCompletedEventArgs e)
        {
            //PauseResumeText = "Pause";
            //SpeechState = _Synth.State;
            UpdateState(GeneratorState.Ready);
        }

        private void _Synth_SpeakStarted(object? sender, SpeakStartedEventArgs e)
        {
            //PauseResumeText = "Pause";
            UpdateState(GeneratorState.Playing);
        }

        private void UpdateState(GeneratorState state)
        {
            this.State = state;
            OnSpeechStateChanged(state);
        }
        #endregion
    }
}
