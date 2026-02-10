using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscilloscopeApp.Models;
using OscilloscopeApp.Services;

namespace OscilloscopeApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SignalGenerator _signalGenerator;
        private readonly DatabaseService _databaseService;
        private readonly System.Timers.Timer _timer;
        private double _currentTime = 0;
        private bool _invalidPromptShown = false;

        [ObservableProperty]
        private SignalSettings _settings;

        [ObservableProperty]
        private ObservableCollection<DataPoint> _wavePoints = new ObservableCollection<DataPoint>();

        [ObservableProperty]
        private double _vpp;

        [ObservableProperty]
        private double _vrms;

        [ObservableProperty]
        private double _avgFreq;

        [ObservableProperty]
        private double _period;

        [ObservableProperty]
        private string _triggerStatus = "Ready";

        public double[] SamplingRates { get; } = { 1000, 5000, 10000, 50000, 100000, 1000000 };
        public double[] TimeBases { get; } = { 0.0001, 0.0002, 0.0005, 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10 };
        public double[] VoltDivs { get; } = { 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50 };
        public double[] DutyCycles { get; } = { 10, 25, 30, 40, 50, 60, 70, 75, 90 };
        public int[] NoiseLevels { get; } = { 0, 1, 3, 5, 10 };

        public class EnumOption<T>
        {
            public T Value { get; set; }
            public string Display { get; set; } = "";
        }

        public List<EnumOption<WaveType>> WaveTypeOptions { get; } = new()
        {
            new EnumOption<WaveType>{ Value = WaveType.Sine, Display = "正弦波" },
            new EnumOption<WaveType>{ Value = WaveType.Square, Display = "方波" },
            new EnumOption<WaveType>{ Value = WaveType.Triangle, Display = "三角波" },
            new EnumOption<WaveType>{ Value = WaveType.Sawtooth, Display = "锯齿波" },
            new EnumOption<WaveType>{ Value = WaveType.Noise, Display = "噪声波" },
        };

        public List<EnumOption<TriggerMode>> TriggerModeOptions { get; } = new()
        {
            new EnumOption<TriggerMode>{ Value = TriggerMode.Auto, Display = "自动" },
            new EnumOption<TriggerMode>{ Value = TriggerMode.Normal, Display = "普通" },
            new EnumOption<TriggerMode>{ Value = TriggerMode.Single, Display = "单次" },
            new EnumOption<TriggerMode>{ Value = TriggerMode.Roll, Display = "滚动" },
        };

        public List<EnumOption<TriggerEdge>> TriggerEdgeOptions { get; } = new()
        {
            new EnumOption<TriggerEdge>{ Value = TriggerEdge.Rising, Display = "上升沿" },
            new EnumOption<TriggerEdge>{ Value = TriggerEdge.Falling, Display = "下降沿" },
        };

        public MainViewModel()
        {
            _signalGenerator = new SignalGenerator();
            _databaseService = new DatabaseService();
            _settings = _databaseService.LoadSettings() ?? new SignalSettings();

            _timer = new System.Timers.Timer(50); // 20 FPS
            _timer.Elapsed += OnTimerElapsed;
            
            StartCommand = new RelayCommand(Start);
            StopCommand = new RelayCommand(Stop);
            SaveCommand = new RelayCommand(SaveSettings);
            SaveDataCommand = new RelayCommand(SaveWaveData);
        }

        public IRelayCommand StartCommand { get; }
        public IRelayCommand StopCommand { get; }
        public IRelayCommand SaveCommand { get; }
        public IRelayCommand SaveDataCommand { get; }

        private void Start()
        {
            _timer.Start();
            TriggerStatus = "Running";
            _invalidPromptShown = false;
        }

        private void Stop()
        {
            _timer.Stop();
            TriggerStatus = "Stopped";
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (Settings == null) return;

            double duration = Settings.TimeBase * 10; 
            
            var newPoints = _signalGenerator.GenerateWave(Settings, _currentTime, duration);
            if (newPoints == null || newPoints.Count == 0)
            {
                Stop();
                if (!_invalidPromptShown)
                {
                    _invalidPromptShown = true;
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("当前参数不合理，无法生成波形。请检查频率、振幅、采样率与时间基准。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
                return;
            }
            
            if (Settings.TriggerMode == TriggerMode.Roll)
            {
                _currentTime += duration;
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    foreach (var p in newPoints)
                    {
                        WavePoints.Add(p);
                    }
                    while (WavePoints.Count > (Settings.SamplingRate * Settings.TimeBase * 10))
                    {
                        WavePoints.RemoveAt(0);
                    }
                    UpdateMeasurements(WavePoints.ToList());
                });
            }
            else
            {
                _currentTime += duration;
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    WavePoints = new ObservableCollection<DataPoint>(newPoints);
                    UpdateMeasurements(newPoints);
                    
                    if (Settings.TriggerMode == TriggerMode.Single && newPoints.Count > 0)
                    {
                        Stop();
                    }
                });
            }
        }

        private void UpdateMeasurements(List<DataPoint> points)
        {
            if (points == null || points.Count < 2 || Settings == null)
            {
                Vpp = 0; Vrms = 0; AvgFreq = 0; Period = 0;
                return;
            }

            double max = points.Max(p => p.Value);
            double min = points.Min(p => p.Value);
            Vpp = max - min;
            Vrms = Math.Sqrt(points.Select(p => p.Value * p.Value).Average());

            int zeroCrossings = 0;
            double firstCrossingTime = -1;
            double lastCrossingTime = -1;
            
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i - 1].Value < 0 && points[i].Value >= 0)
                {
                    if (firstCrossingTime < 0) firstCrossingTime = points[i].Time;
                    lastCrossingTime = points[i].Time;
                    zeroCrossings++;
                }
            }

            if (zeroCrossings > 1)
            {
                Period = (lastCrossingTime - firstCrossingTime) / (zeroCrossings - 1);
                AvgFreq = 1.0 / Period;
            }
            else
            {
                AvgFreq = Settings.Frequency;
                Period = Settings.Frequency > 0 ? 1.0 / Settings.Frequency : 0;
            }
        }

        private void SaveSettings()
        {
            if (Settings != null)
            {
                _databaseService.SaveSettings(Settings);
                System.Windows.MessageBox.Show("参数已保存到数据库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveWaveData()
        {
            if (WavePoints != null && WavePoints.Count > 0)
            {
                _databaseService.SaveWaveData(WavePoints);
                System.Windows.MessageBox.Show("波形数据已保存到数据库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("无可保存的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
