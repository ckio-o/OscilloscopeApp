using System;

using CommunityToolkit.Mvvm.ComponentModel;

namespace OscilloscopeApp.Models
{
    public enum WaveType
    {
        Sine,
        Square,
        Triangle,
        Sawtooth,
        Noise
    }

    public enum TriggerMode
    {
        Auto,
        Normal,
        Single,
        Roll
    }

    public enum TriggerEdge
    {
        Rising,
        Falling
    }

    public partial class SignalSettings : ObservableObject
    {
        [ObservableProperty]
        private WaveType _waveType = WaveType.Sine;

        [ObservableProperty]
        private double _frequency = 0;

        [ObservableProperty]
        private double _amplitude = 0;

        [ObservableProperty]
        private double _dutyCycle = 0;

        [ObservableProperty]
        private int _noiseLevel = 0;

        [ObservableProperty]
        private double _samplingRate = 1000;

        [ObservableProperty]
        private double _timeBase = 0.01;

        [ObservableProperty]
        private double _voltDiv = 50;

        [ObservableProperty]
        private TriggerMode _triggerMode = TriggerMode.Auto;

        [ObservableProperty]
        private TriggerEdge _triggerEdge = TriggerEdge.Rising;

        [ObservableProperty]
        private double _triggerLevel = 0;

        partial void OnAmplitudeChanged(double value)
        {
            if (Math.Abs(TriggerLevel) > value)
            {
                TriggerLevel = Math.Sign(TriggerLevel) * value;
            }
        }
    }

    public class DataPoint
    {
        public double Time { get; set; }
        public double Value { get; set; }
    }
}
