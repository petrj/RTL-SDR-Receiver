using System;
using System.Collections.Generic;
using Terminal.Gui;
using NStack;
using LoggerService;
using RTLSDR.Audio;
using RTLSDR;

namespace RadI0;

public class Rad10GUI
{
    // Gain settings
    private static string gainMode = "SW auto";
    private static int manualGainValue = 0;
    private Dictionary<int,Station>? _stations = new Dictionary<int, Station>();

    private ListView? _stationList;
    private Label? _statusValueLabel;
    private Label? _frequencyValueLabel;
    private Label? _bitrateValueLabel;
    private Label? _audoBitrateValueLabel;
    private Label? _deviceValueLabel;
    private Label? _audioValueLabel;
    private Label? _syncValueLabel;
    private Label? _gainValueLabel;
    private RadioGroup? _bandSelector;

    public event EventHandler OnStationChanged = null;
    public event EventHandler OnQuit = null;

    public void RefreshStations(List<Station> stations, Station? selectedStation = null)
    {
        if (_stationList == null)
            return;

        // Update the UI safely
        Application.MainLoop.Invoke(() =>
        {
            var stationDisplay = new List<string>();

            _stations.Clear();
            int selectedItem = 0;

            var i = 0;
            foreach (var s in stations)
            {
                stationDisplay.Add($"{s.ServiceNumber,5} | {s.Name}");
                _stations.Add(i,s);
                if (selectedStation != null && selectedStation.ServiceNumber == s.ServiceNumber)
                {
                    selectedItem = i;
                }
                i++;
            }

            _stationList.SetSource(stationDisplay);
            _stationList.SelectedItem = selectedItem;

        });
    }

    public void RefreshStat(string status,
        string bitRate,
        string frequency,
        string device,
        string audio,
        string synced,
        string gain,
        string audioBitRate)
    {
        if (_frequencyValueLabel == null)
        return;


        Application.MainLoop.Invoke(() =>
        {
            _frequencyValueLabel.Text = frequency;
            _statusValueLabel.Text = status;
            _bitrateValueLabel.Text = bitRate;
            _deviceValueLabel.Text = device;
            _audioValueLabel.Text = audio;
            _syncValueLabel.Text = synced;
            _gainValueLabel.Text = gain;
            _audoBitrateValueLabel.Text = audioBitRate;
        });
    }

    public void RefreshBand(bool FM)
    {
        Application.MainLoop.Invoke(() =>
        {
            _bandSelector.SelectedItem = FM ? 0 : 1;
        });
    }

    public void Run()
    {
        Application.Init();
        Toplevel top = Application.Top;

        int frameHeight = 20;

        var window = new Window("Rad10 - DAB+/FM Radio Player")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var displyFrame = CreateDisplayFrame(out Label displayLabel);

        // stations frame
        var stationFrame = CreateStationsFrame(out ListView stationList, frameHeight);
        _stationList = stationList;

        // status frame
        var statusFrame = CreateStatusFrame(out Label statusValueLabel, out Label frequencyValueLabel,
                                            out Label bitrateValueLabel, out Label deviceValueLabel,
                                            out Label gainValueLabel,
                                            10);

        var demodStatusFrame = CreateDemodulatorStatusFrame(
            out Label audioValueLabel,
            out Label syncValueLabel,
            out Label audioBitrateValueLabel);

        _statusValueLabel = statusValueLabel;
        _frequencyValueLabel = frequencyValueLabel;
        _bitrateValueLabel = bitrateValueLabel;
        _deviceValueLabel = deviceValueLabel;
        _audioValueLabel = audioValueLabel;
        _syncValueLabel = syncValueLabel;
        _gainValueLabel  = gainValueLabel;
        _audoBitrateValueLabel = audioBitrateValueLabel;

        // controls frame
        var controlsFrame = CreateControlsFrame();

        // window
        window.Add(stationFrame);
        window.Add(statusFrame);
        window.Add(demodStatusFrame);
        window.Add(controlsFrame);
        window.Add(displyFrame);
        top.Add(window);

        // ===== Station selection change =====
        stationList.SelectedItemChanged += args =>
        {

            //if (!customFreqActive)
              //  UpdateDynamicValues(args.Item);
        };

        // ===== Activation =====
        stationList.OpenSelectedItem += args =>
        {
            if (OnStationChanged!= null )
            {
                var itmIndex = _stationList.SelectedItem;
                var station = _stations[itmIndex];

                if (station == null)
                    return;

                OnStationChanged(this, new StationFoundEventArgs()
                {
                    Station = station
                });
            }
        };

/*
        // ===== Set Freq button =====
        setFreqButton.Clicked += () =>
        {
            var input = new TextField("") { X = 1, Y = 1, Width = 15 };
            var dlg = new Dialog("Enter frequency (Hz)", 30, 5);
            dlg.Add(input);

            var okButton = new Button("OK", is_default: true);
            okButton.Clicked += () =>
            {

                if (long.TryParse(input.Text.ToString(), out long freqHz))
                {
                    frequencyValueLabel.Text = $"{freqHz} Hz";
                    customFreqActive = true;
                }

                Application.RequestStop();
            };
            dlg.AddButton(okButton);

            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();
            dlg.AddButton(cancelButton);

            Application.Run(dlg);
        };
*/
        // ===== Quit button =====


        Application.Run();
        Application.Shutdown();
    }

    private static FrameView CreateDisplayFrame(out Label displayLabel)
    {
         var frame = new FrameView("") { X = 0, Y = 0, Width = 78, Height = 3 };
         displayLabel = new Label("---") { X = 0, Y = 0 };

        frame.Add(displayLabel);
        return frame;
    }

    // ===== Create Stations frame =====
        private static FrameView CreateStationsFrame(out ListView stationList, int frameHeight)
        {
            stationList = new ListView(new List<string>()) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
            var frame = new FrameView("Stations") { X = 0, Y = 3, Width = 28, Height = 18 };
            frame.Add(stationList);
            return frame;
        }

        // ===== Create Status frame =====
        private static FrameView CreateStatusFrame(out Label statusValueLabel, out Label frequencyValueLabel,
                                                   out Label bitrateValueLabel, out Label deviceValueLabel,
                                                   out Label gainValueLabel,
                                                   int frameHeight)
        {
            var frame = new FrameView("RTL SDR driver") { X = 28, Y = 3, Width = 35, Height = 10 };

            var statusLabel = new Label("State:") { X = 1, Y = 1 };
            var deviceLabel = new Label("Device:")   { X = 1, Y = 2 };
            var bitrateLabel = new Label("Bitrate:") { X = 1, Y = 3 };
            var frequencyLabel = new Label("Freq:") { X = 1, Y = 4 };
            var gainLabel = new Label("Gain:") { X = 1, Y = 5 };

            statusValueLabel = new Label("---") { X = 10, Y = 1 };
            deviceValueLabel = new Label("---") { X = 10, Y = 2 };
            bitrateValueLabel = new Label("---") { X = 10, Y = 3 };
            frequencyValueLabel = new Label("---") { X = 10, Y = 4 };
            gainValueLabel = new Label("---") { X = 10, Y = 5 };

            frame.Add(statusLabel, statusValueLabel,
                      frequencyLabel, frequencyValueLabel,
                      bitrateLabel, bitrateValueLabel,
                      deviceLabel, deviceValueLabel,
                      gainLabel, gainValueLabel);

            return frame;
        }

        // ===== Create Audio Status frame =====
        private static FrameView CreateDemodulatorStatusFrame(out Label audioValueLabel,
                                                    out Label syncValueLabel,
                                                    out Label audioBitRateValueLabel)
        {
            var frame = new FrameView("DAB/FM") { X = 28, Y = 13, Width = 35, Height = 8 };

            var audioLabel = new Label("Audio:") { X = 1, Y = 1 };
            var audioBitrateLabel = new Label("Bitrate:") { X = 1, Y = 2 };
            var syncLabel = new Label("Sync:") { X = 1, Y = 5 };

            audioValueLabel = new Label("---") { X = 10, Y = 1 };
            audioBitRateValueLabel = new Label("---") { X = 10, Y = 2 };
            syncValueLabel = new Label("---") { X = 10, Y = 5 };

            frame.Add(audioLabel, audioValueLabel,
                      audioBitrateLabel,audioBitRateValueLabel,
                      syncLabel, syncValueLabel);

            return frame;
        }

        // ===== Create Controls frame =====
        private FrameView CreateControlsFrame()
        {
            var frame = new FrameView("") { X = 63, Y = 3, Width = 15, Height = 18 };

           _bandSelector = new RadioGroup(new ustring[] { ustring.Make("FM"), ustring.Make("DAB") }) { X = 1, Y = 0, SelectedItem = 1 };

            var quitButton = new Button("Quit") { X = 1, Y = 15 };
            quitButton.Clicked += () =>
            {
                if (OnQuit != null)
                {
                    OnQuit(this, new EventArgs());
                }
                Application.RequestStop();
            };

            var setFreqButton = new Button("Freq") { X = 1, Y = 3 };
            var tuneButton = new Button("Tune") { X = 1, Y = 4 };
            var gainButton = new Button("Gain") { X = 1, Y = 6 };

            // ===== Gain button =====
            gainButton.Clicked += () =>
            {
                // Dialog to select mode
                var options = new List<string> { "SW auto", "HW auto", "Manual" };
                int selected = 0;

                var modeDlg = new Dialog("Select Gain Mode", 30, 8);
                var radio = new RadioGroup(new ustring[] { "SW auto", "HW auto", "Manual" }) { X = 1, Y = 1, SelectedItem = 0 };
                modeDlg.Add(radio);

                var okMode = new Button("OK", is_default: true);
                okMode.Clicked += () =>
                {
                    selected = radio.SelectedItem;
                    gainMode = options[selected];

                    if (gainMode == "Manual")
                    {
                        // Ask for manual integer value
                        var valDlg = new Dialog("Enter Manual Gain", 30, 5);
                        var input = new TextField("") { X = 1, Y = 1, Width = 10 };
                        valDlg.Add(input);

                        var okVal = new Button("OK", is_default: true);
                        okVal.Clicked += () =>
                        {
                            if (int.TryParse(input.Text.ToString(), out int v))
                                manualGainValue = v;
                            Application.RequestStop();
                        };
                        valDlg.AddButton(okVal);

                        var cancelVal = new Button("Cancel");
                        cancelVal.Clicked += () => Application.RequestStop();
                        valDlg.AddButton(cancelVal);

                        Application.Run(valDlg);
                    }

                    Application.RequestStop();
                };
                modeDlg.AddButton(okMode);

                var cancelMode = new Button("Cancel");
                cancelMode.Clicked += () => Application.RequestStop();
                modeDlg.AddButton(cancelMode);

                Application.Run(modeDlg);
            };


            var recButton = new Button("Record") { X = 1, Y = 7 };

            frame.Add(_bandSelector, setFreqButton, quitButton, gainButton, tuneButton, recButton);

            return frame;
        }
}
