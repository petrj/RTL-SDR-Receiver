using System;
using System.Collections.Generic;
using Terminal.Gui;
using NStack;
using LoggerService;
using RTLSDR.Audio;
using RTLSDR;
using RTLSDR.Common;

namespace RadI0;

public class RadI0GUI
{
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
    private Label? _queueValueLabel;
    private Label? _displayLabel;
    private Label? _statLabel;
    private Label? _spectrumLabel;

    private Window? _window;
    private Label? _indicatorLabel;

    public event EventHandler OnStationChanged = null;
    public event EventHandler OnGainChanged = null;
    public event EventHandler OnFrequentionChanged = null;
    public event EventHandler OnQuit = null;

    public event EventHandler OnBandchanged = null;

    public event EventHandler OnRecordStart = null;
    public event EventHandler OnRecordStop = null;

    public event EventHandler OnTuningStart = null;
    public event EventHandler OnTuningStop = null;
    private bool _autoSettingBand = false;

    public void RefreshStations(List<Station> stations, Station? selectedStation = null)
    {
        if (_stationList == null)
            return;

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

        // Update the UI safely
        Application.MainLoop.Invoke(() =>
        {
            _stationList.SetSource(stationDisplay);
            _stationList.SelectedItem = selectedItem;
        });
    }

    public int SpectrumWidth
    {
        get
        {
            if (_spectrumLabel == null)
            return 0;

            int w;
             _spectrumLabel.GetCurrentWidth(out w);
             return w-2;
        }
    }

    public int SpectrumHeight
    {
        get
        {
            if (_spectrumLabel == null)
            return 0;

            int h;
             _spectrumLabel.GetCurrentHeight(out h);
             return h-2;
        }
    }

    public void RefreshStat(AppStatus status)
    {
        if (_frequencyValueLabel == null)
        return;

        Application.MainLoop.Invoke(() =>
        {
            _frequencyValueLabel.Text = status.Frequency;
            _statusValueLabel.Text = status.Status;
            _bitrateValueLabel.Text = status.BitRate;
            _deviceValueLabel.Text = status.Device;
            _audioValueLabel.Text = status.Audio;
            _syncValueLabel.Text = status.Synced;
            _gainValueLabel.Text = status.Gain;
            _audoBitrateValueLabel.Text = status.AudioBitRate;
            _queueValueLabel.Text = status.Queue;
            _displayLabel.Text = status.DisplayText;
            _indicatorLabel.Text = status.Indicator;

            if (_statLabel != null)
            {
                _statLabel.Text = status.Stat;
            }
            if (_spectrumLabel != null)
            {
                _spectrumLabel.Text = status.Spectrum;
            }
        });
    }

    public void RefreshBand(bool FM)
    {
        Application.MainLoop.Invoke(() =>
        {
            _autoSettingBand = true;
            _bandSelector.SelectedItem = FM ? 0 : 1;
            _autoSettingBand = false;
        });
    }

    public bool StatWindowActive
    {
        get
        {
            return !(_statLabel == null);
        }
    }

    public void Run()
    {
        Application.Init();
        Toplevel top = Application.Top;

        int frameHeight = 20;

        _window = new Window("RadI0 - DAB+/FM Radio Player")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var displyFrame = CreateDisplayFrame();

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
        _window.Add(stationFrame);
        _window.Add(statusFrame);
        _window.Add(demodStatusFrame);
        _window.Add(controlsFrame);
        _window.Add(displyFrame);
        top.Add(_window);

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

        Application.Run();
        Application.Shutdown();
    }

    public void SetTitle(string title)
    {
        _window.Text = title;
    }

    private FrameView CreateDisplayFrame()
    {
         var frame = new FrameView("") { X = 0, Y = 0, Width = Dim.Fill(), Height = 3 };
         _displayLabel = new Label("---") { X = 1, Y = 0 };

        frame.Add(_displayLabel);
        return frame;
    }

    // ===== Create Stations frame =====
        private static FrameView CreateStationsFrame(out ListView stationList, int frameHeight)
        {
            stationList = new ListView(new List<string>()) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
            var frame = new FrameView("Stations") { X = 0, Y = 3, Width = Dim.Fill(50), Height = Dim.Fill() };
            frame.Add(stationList);
            return frame;
        }

        // ===== Create Status frame =====
        private static FrameView CreateStatusFrame(out Label statusValueLabel, out Label frequencyValueLabel,
                                                   out Label bitrateValueLabel, out Label deviceValueLabel,
                                                   out Label gainValueLabel,
                                                   int frameHeight)
        {
            var frame = new FrameView("RTL SDR driver") { X = Pos.AnchorEnd(50), Y = 3, Width = Dim.Fill(15), Height = 8 };

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
        private FrameView CreateDemodulatorStatusFrame(out Label audioValueLabel,
                                                    out Label syncValueLabel,
                                                    out Label audioBitRateValueLabel)
        {
            var frame = new FrameView("DAB/FM demodulator") { X = Pos.AnchorEnd(50), Y = 11, Width = Dim.Fill(15), Height = Dim.Fill() };

            var audioLabel = new Label("Audio:") { X = 1, Y = 1 };
            var audioBitrateLabel = new Label("Bitrate:") { X = 1, Y = 2 };
            var queueLabel = new Label("Queue:") { X = 1, Y = 3 };

            var syncLabel = new Label("Synced:") { X = 1, Y = 7 };

            audioValueLabel = new Label("---") { X = 10, Y = 1 };
            audioBitRateValueLabel = new Label("---") { X = 10, Y = 2 };

            _queueValueLabel = new Label("---") { X = 10, Y = 3 };
            _indicatorLabel = new Label("") { X = 10, Y = 5 };
            syncValueLabel = new Label("---") { X = 10, Y = 7 };

            frame.Add(audioLabel, audioValueLabel,
                      audioBitrateLabel,audioBitRateValueLabel,
                      queueLabel, _queueValueLabel,
                      _indicatorLabel,
                      syncLabel, syncValueLabel);

            return frame;
        }

    double? ShowFMChooseDecimalPartDialog(int baseValue)
    {
        double? result = null;

        var values = Enumerable.Range(0, 10)
                            .Select(i => $"{baseValue}.{i}")
                            .ToList();

        var list = new ListView(values)
        {
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2
        };

        list.SelectedItem = 0;

        var ok = new Button("OK");
        var cancel = new Button("Cancel");

        ok.Clicked += () =>
        {
            result = double.Parse(values[list.SelectedItem]);
            Application.RequestStop();
        };
        list.OpenSelectedItem += (args) =>
        {
            ok.OnClicked();
        };

        cancel.Clicked += () => Application.RequestStop();

        var dlg = new Dialog($"Choose {baseValue}.x", 40, 15, ok, cancel)
        {
            X = 40,
            Y = 2
        };

        dlg.Add(list);

        dlg.Loaded += () => list.SetFocus();

        Application.Run(dlg);
        dlg.Dispose();

        if (result.HasValue)
        {
            if (OnFrequentionChanged != null)
            {
                OnFrequentionChanged(this, new FrequentionChangedEventArgs()
                {
                    Frequention = Convert.ToInt32(result.Value*1000000) // in Hz
                });
            }
        }

        return result;
    }

    int? ShowFMChooseIntegerPartDialog()
    {
        int? result = null;

        var values = Enumerable.Range(88, 21) // 88..108
                            .Select(v => v.ToString())
                            .ToList();

        var list = new ListView(values)
        {
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2
        };

        list.SelectedItem = 0;

        var ok = new Button("OK");
        var cancel = new Button("Cancel");

        ok.Clicked += () =>
        {
            result = int.Parse(values[list.SelectedItem]);
            Application.RequestStop();
        };

        list.OpenSelectedItem += (args) =>
        {
            ok.OnClicked();
        };

        cancel.Clicked += () => Application.RequestStop();

        var dlg = new Dialog("Choose base value", 40, 20, ok, cancel)
        {
            X=40,
            Y=2
        };

        dlg.Add(list);

        dlg.Loaded += () => list.SetFocus();

        Application.Run(dlg);
        dlg.Dispose();

        return result;
    }

    private void ChooseDABFreq()
    {
        var menuItems = new List<string>();
        foreach (var dabFreq in AudioTools.DabFrequenciesHz)
        {
            menuItems.Add(dabFreq.Key);
        }

        var okButton= new Button("OK", is_default: true);
        var cancelButton= new Button("Cancel", is_default: true);

        cancelButton.Clicked += () =>
        {
            Application.RequestStop();
        };

        var dialog = new Dialog("Select Frequency", 30, 15, okButton, cancelButton)
        {
            X = 40,
            Y = 2
        };
        var freqList = new ListView(menuItems) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() - 2, SelectedItem = 0 };

        dialog.Add(freqList);

        dialog.Loaded += () =>
        {
            freqList.SetFocus();
        };

        okButton.Clicked += () =>
        {
            //result = int.Parse(numbers[listView.SelectedItem]);
            var res =  menuItems[freqList.SelectedItem];
            var freq = AudioTools.ParseFreq(res);

            if (freq <=0)
            return;

            if (OnFrequentionChanged != null)
            {
                OnFrequentionChanged(this, new FrequentionChangedEventArgs()
                {
                    Frequention = freq
                });
            }

            Application.RequestStop();
        };

        Application.Run(dialog);
        dialog.Dispose();
    }

        private void OnFreqClicked(RadioGroup bandSelector)
        {
            if (bandSelector.SelectedItem == 0)
            {
                // FM
                var baseValue =  ShowFMChooseIntegerPartDialog();
                if (!baseValue.HasValue)
                    return;

                ShowFMChooseDecimalPartDialog(baseValue.Value);

            } else
            {
                // DAB
                ChooseDABFreq();
            }
        }

        private void OnTuneClicked()
        {
            if (_indicatorLabel.Text.ToLower().Contains("tun"))
            {
                // stop tuning
                if (OnTuningStop != null)
                {
                    OnTuningStop(this, new EventArgs());
                }

            } else
            {
                // start tuning
                if (OnTuningStart != null)
                {
                    OnTuningStart(this, new EventArgs());
                }
            }
        }

        private void OnRecordClicked()
        {
            if (_indicatorLabel.Text.ToLower().Contains("rec"))
            {
                // stop recording

                int result = MessageBox.Query(
                    "Confirm",
                    "Are you sure to stop recording?",
                    "Yes",
                    "No"
                );

                if (result == 0)
                {
                    // User pressed "Yes"
                    if (OnRecordStop != null)
                    {
                        OnRecordStop(this, new EventArgs());
                    }
                }
                else
                {
                    // User pressed "No" (or Esc)
                }

            } else
            {
                // start record
                if (OnRecordStart != null)
                {
                    OnRecordStart(this, new EventArgs());
                }
            }
        }

        private void OnStatClicked()
        {
            if (_statLabel == null)
            {
                _statLabel = new Label("")
                {
                    X = 0,
                    Y = 0,
                    Width = Dim.Fill(),
                    Height = Dim.Fill() - 2,
                    AutoSize = false,
                    TextAlignment = TextAlignment.Left
                };
            }

            var closeButton = new Button("Close", is_default: true);
            closeButton.Clicked += () => Application.RequestStop();

            var modeDlg = new Dialog("Stat", 70, 20, closeButton)
            {
                X = Pos.At(5),
                Y = Pos.At(2),
                Width = Dim.Fill(5),   // Fill available width, leaving a margin
                Height = Dim.Fill(2),  // Fill available height, leaving a margin
            };

            modeDlg.Add(_statLabel);

            Application.Run(modeDlg);
            modeDlg.Dispose();
            _statLabel = null;
        }

        private void OnSpectrumClicked()
        {
            if (_spectrumLabel == null)
            {
                _spectrumLabel = new Label("")
                {
                    X = 0,
                    Y = 0,
                    Width = Dim.Fill(),
                    Height = Dim.Fill() - 2,
                    AutoSize = false,
                    TextAlignment = TextAlignment.Left
                };
            }

            var closeButton = new Button("Close", is_default: true);
            closeButton.Clicked += () => Application.RequestStop();

            var modeDlg = new Dialog("Spectrum", 70, 22, closeButton)
            {
                X = Pos.At(5),
                Y = Pos.At(2),
                Width = Dim.Fill(5),   // Fill available width, leaving a margin
                Height = Dim.Fill(2),  // Fill available height, leaving a margin
            };

            modeDlg.Add(_spectrumLabel);

            Application.Run(modeDlg);
            modeDlg.Dispose();
            _spectrumLabel = null;
        }

        private void OnGainClicked()
        {
            // Dialog to select mode
            var options = new List<string> { "SW auto", "HW auto", "Manual" };
            int selected = 0;

            var list = new ListView(options)
            {
                Width = Dim.Fill(),
                Height = Dim.Fill() - 2
            };

            var okButton = new Button("OK", is_default: true);
            okButton.Clicked += () =>
            {
                selected = list.SelectedItem;
                var gainMode = options[selected];

                if (gainMode == "Manual")
                {
                    var input = new TextField("") { X = 1, Y = 1, Width = 15 };

                    var okVal = new Button("OK", is_default: true);
                    okVal.Clicked += () =>
                    {
                        if (int.TryParse(input.Text.ToString(), out int v))

                            if (OnGainChanged != null)
                            {
                                OnGainChanged(this, new GainChangedEventArgs()
                                {
                                    ManualGainValue = v
                                });
                            }

                        Application.RequestStop();
                    };

                    var cancelVal = new Button("Cancel");
                    cancelVal.Clicked += () => Application.RequestStop();

                    // Ask for manual integer value
                    var valDlg = new Dialog($"Enter gain (10th of dB)", 30, 7, okVal, cancelVal)
                    {
                        X = 40,
                        Y = 3
                    };

                    valDlg.Add(input);

                    valDlg.Loaded += () => input.SetFocus();

                    Application.Run(valDlg);
                } else if (gainMode == "HW auto")
                {
                    if (OnGainChanged != null)
                    {
                        OnGainChanged(this, new GainChangedEventArgs()
                        {
                            HWGain = true
                        });
                    }
                }
                else if (gainMode == "SW auto")
                {
                    if (OnGainChanged != null)
                    {
                        OnGainChanged(this, new GainChangedEventArgs()
                        {
                            SWGain = true
                        });
                    }
                }

                Application.RequestStop();
            };

            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var modeDlg = new Dialog("Select Gain Mode", 30, 10, okButton, cancelButton)
            {
                X = 30,
                Y = 2
            };

            modeDlg.Loaded += () => list.SetFocus();

            list.OpenSelectedItem += (args) =>
            {
                okButton.OnClicked();
            };

            modeDlg.Add(list);

            Application.Run(modeDlg);
        }

    // ===== Create Controls frame =====
    private FrameView CreateControlsFrame()
    {
        var frame = new FrameView("")
        {
            X = Pos.AnchorEnd(15),
            Y = 3,
            Width = 15,
            Height = Dim.Fill()
        };

        _bandSelector = new RadioGroup(new ustring[] { ustring.Make("FM"), ustring.Make("DAB") }) { X = 1, Y = 0, SelectedItem = 1 };

        _bandSelector.SelectedItemChanged += (ea) =>
        {
            HandleBandChange(ea.SelectedItem);
        };

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
        var recButton = new Button("Record") { X = 1, Y = 7 };

        var statButton = new Button("Stat") { X = 1, Y = 11 };
        var spectrumButton = new Button("Spectrum") { X = 1, Y = 12 };

        recButton.Clicked +=() => OnRecordClicked();
        gainButton.Clicked += () => OnGainClicked();
        setFreqButton.Clicked += () => OnFreqClicked(_bandSelector);
        tuneButton.Clicked += () => OnTuneClicked();
        statButton.Clicked += () => OnStatClicked();
        spectrumButton.Clicked += () => OnSpectrumClicked();

        frame.Add(_bandSelector, setFreqButton,
            tuneButton, gainButton, recButton,
            statButton, spectrumButton,
            quitButton);

        return frame;
    }

    private void HandleBandChange(int index)
    {
        if ((!_autoSettingBand) && (OnBandchanged != null))
        {
            if (MessageBox.Query(
                "Confirm",
                "Are you sure to change band to " + (index == 0 ? "FM" : "DAB") + "?",
                "Yes",
                "No"
            ) == 0)
            {
                OnBandchanged(this, new BandChangedEventArgs() { FM = (index == 0 )});
            }
        }
    }

        public void ShowInfoDialog(string info)
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.Query(
                    "Info",
                    info,
                    "OK"
                );
            });
        }
}
