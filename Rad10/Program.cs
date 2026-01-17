using System;
using System.Collections.Generic;
using Terminal.Gui;
using NStack;

namespace DigitalRadioTui
{
    internal class Program
    {
        private static bool isPlaying = false;
        private static int currentBand = 1; // 0 = FM, 1 = DAB
        private static bool customFreqActive = false;

        // Gain settings
        private static string gainMode = "SW auto";
        private static int manualGainValue = 0;

        private class Station
        {
            public string Name { get; set; }
            public int ServiceNumber { get; set; }

            public Station(string name, int serviceNumber)
            {
                Name = name;
                ServiceNumber = serviceNumber;
            }
        }

        private static readonly List<Station> stations = new();
        private static readonly List<string> stationDisplay = new();

        static void Main()
        {
            Application.Init();
            Toplevel top = Application.Top;

            int frameHeight = 16;

            var window = new Window("Digital Radio")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // ===== Create frames =====
            var stationFrame = CreateStationsFrame(out ListView stationList, frameHeight);
            var statusFrame = CreateStatusFrame(out Label statusValueLabel, out Label frequencyValueLabel,
                                                out Label bitrateValueLabel, out Label deviceValueLabel,
                                                out Label audioValueLabel, out CheckBox syncCheckBox,
                                                frameHeight);
            var controlsFrame = CreateControlsFrame(out RadioGroup bandSelector, out Button setFreqButton,
                                                    out Button quitButton, out Button gainButton, frameHeight);

            // ===== Add frames to window =====
            window.Add(stationFrame);
            window.Add(statusFrame);
            window.Add(controlsFrame);
            top.Add(window);

            // ===== Initialize stations =====
            void RefreshStationDisplay()
            {
                stationDisplay.Clear();
                foreach (var s in stations)
                    stationDisplay.Add($"{s.ServiceNumber,3} | {s.Name}");
                stationList.SetSource(stationDisplay);
                stationList.SelectedItem = 0;
            }

            void UpdateDynamicValues(int index)
            {
                if (customFreqActive) return;
                if (index < 0 || index >= stations.Count)
                {
                    frequencyValueLabel.Text = "---";
                    statusValueLabel.Text = "STOPPED";
                    bitrateValueLabel.Text = "---";
                    deviceValueLabel.Text = "---";
                    audioValueLabel.Text = "---";
                    syncCheckBox.Checked = false;
                    return;
                }

                var s = stations[index];
                if (currentBand == 0)
                    frequencyValueLabel.Text = index switch
                    {
                        0 => "88.5 MHz",
                        1 => "101.2 MHz",
                        2 => "104.8 MHz",
                        _ => "---"
                    };
                else
                    frequencyValueLabel.Text = "12C (227.36 MHz)";

                statusValueLabel.Text = isPlaying ? "PLAYING" : "STOPPED";
                bitrateValueLabel.Text = currentBand == 0 ? "128 kbps" : "256 kbps";
                deviceValueLabel.Text = currentBand == 0 ? "FM Tuner" : "DAB Tuner";
                audioValueLabel.Text = currentBand == 0 ? "Stereo" : "Digital";
                syncCheckBox.Checked = isPlaying;
            }

            // ===== Band change =====
            bandSelector.SelectedItemChanged += args =>
            {
                currentBand = args.SelectedItem;
                isPlaying = false;
                customFreqActive = false;

                stations.Clear();
                if (currentBand == 0)
                {
                    stations.Add(new Station("FM 88.5", 1));
                    stations.Add(new Station("FM 101.2", 2));
                    stations.Add(new Station("FM 104.8", 3));
                }
                else
                {
                    stations.Add(new Station("Radiožurnál", 101));
                    stations.Add(new Station("Vltava", 102));
                    stations.Add(new Station("Wave", 103));
                }

                RefreshStationDisplay();
                UpdateDynamicValues(stationList.SelectedItem);
            };

            // ===== Station selection change =====
            stationList.SelectedItemChanged += args =>
            {
                if (!customFreqActive)
                    UpdateDynamicValues(args.Item);
            };

            // ===== Activation =====
            stationList.OpenSelectedItem += args =>
            {
                isPlaying = true;
                UpdateDynamicValues(stationList.SelectedItem);
            };

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

            // ===== Quit button =====
            quitButton.Clicked += () => Application.RequestStop();

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

            bandSelector.SelectedItem = 1; // init
            Application.Run();
            Application.Shutdown();
        }

        // ===== Create Stations frame =====
        private static FrameView CreateStationsFrame(out ListView stationList, int frameHeight)
        {
            stationList = new ListView(new List<string>()) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
            var frame = new FrameView("Stations") { X = 1, Y = 1, Width = 30, Height = frameHeight };
            frame.Add(stationList);
            return frame;
        }

        // ===== Create Status frame =====
        private static FrameView CreateStatusFrame(out Label statusValueLabel, out Label frequencyValueLabel,
                                                   out Label bitrateValueLabel, out Label deviceValueLabel,
                                                   out Label audioValueLabel, out CheckBox syncCheckBox,
                                                   int frameHeight)
        {
            var frame = new FrameView("Status") { X = 32, Y = 1, Width = 40, Height = frameHeight };

            var statusLabel = new Label("Status:") { X = 1, Y = 1 };
            var frequencyLabel = new Label("Frequency:") { X = 1, Y = 2 };
            var bitrateLabel = new Label("Bitrate:") { X = 1, Y = 4 };
            var deviceLabel = new Label("Device:") { X = 1, Y = 5 };
            var audioLabel = new Label("Audio:") { X = 1, Y = 6 };
            var syncLabel = new Label("Synchronized:") { X = 1, Y = 8 };
            syncCheckBox = new CheckBox("") { X = 15, Y = 8, Checked = false };

            statusValueLabel = new Label("STOPPED") { X = 15, Y = 1 };
            frequencyValueLabel = new Label("---") { X = 15, Y = 2 };
            bitrateValueLabel = new Label("---") { X = 15, Y = 4 };
            deviceValueLabel = new Label("---") { X = 15, Y = 5 };
            audioValueLabel = new Label("---") { X = 15, Y = 6 };

            frame.Add(statusLabel, statusValueLabel,
                      frequencyLabel, frequencyValueLabel,
                      bitrateLabel, bitrateValueLabel,
                      deviceLabel, deviceValueLabel,
                      audioLabel, audioValueLabel,
                      syncLabel, syncCheckBox);

            return frame;
        }

        // ===== Create Controls frame =====
        private static FrameView CreateControlsFrame(out RadioGroup bandSelector, out Button setFreqButton,
                                                     out Button quitButton, out Button gainButton, int frameHeight)
        {
            var frame = new FrameView("Controls") { X = 74, Y = 1, Width = 30, Height = frameHeight };

            var bandLabel = new Label("Band") { X = 1, Y = 4 };
            bandSelector = new RadioGroup(new ustring[] { ustring.Make("FM"), ustring.Make("DAB") }) { X = 1, Y = 5, SelectedItem = 1 };

            setFreqButton = new Button("Set Freq") { X = 1, Y = 1 };
            quitButton = new Button("Quit") { X = 1, Y = 3 };
            gainButton = new Button("Gain") { X = 1, Y = 7 };

            frame.Add(bandLabel, bandSelector, setFreqButton, quitButton, gainButton);

            return frame;
        }
    }
}
