using System;
using System.Collections.Generic;
using OscilloscopeApp.Models;

namespace OscilloscopeApp.Services
{
    public class SignalGenerator
    {
        private Random _random = new Random();

        public List<DataPoint> GenerateWave(SignalSettings settings, double startTime, double duration)
        {
            var points = new List<DataPoint>();
            if (settings == null) return points;
            if (settings.SamplingRate <= 0 || duration <= 0) return points;
            if (settings.WaveType != WaveType.Noise && (settings.Frequency <= 0 || settings.Amplitude <= 0)) return points;
            double dt = 1.0 / settings.SamplingRate;
            int count = (int)(duration / dt);

            for (int i = 0; i < count; i++)
            {
                double t = startTime + i * dt;
                double value = 0;

                switch (settings.WaveType)
                {
                    case WaveType.Sine:
                        value = settings.Amplitude * Math.Sin(2 * Math.PI * settings.Frequency * t);
                        break;
                    case WaveType.Square:
                        double period = 1.0 / settings.Frequency;
                        double position = (t % period) / period;
                        value = position < (settings.DutyCycle / 100.0) ? settings.Amplitude : -settings.Amplitude;
                        break;
                    case WaveType.Triangle:
                        double pTriangle = 1.0 / settings.Frequency;
                        double posTriangle = (t % pTriangle) / pTriangle;
                        value = 2 * settings.Amplitude * Math.Abs(2 * (posTriangle - Math.Floor(posTriangle + 0.5))) - settings.Amplitude;
                        break;
                    case WaveType.Sawtooth:
                        double pSaw = 1.0 / settings.Frequency;
                        value = 2 * settings.Amplitude * (t * settings.Frequency - Math.Floor(t * settings.Frequency + 0.5));
                        break;
                    case WaveType.Noise:
                        value = (settings.NoiseLevel / 10.0) * settings.Amplitude * (_random.NextDouble() * 2 - 1);
                        break;
                }

                if (settings.WaveType != WaveType.Noise && settings.NoiseLevel > 0)
                {
                    value += (settings.NoiseLevel / 10.0) * (settings.Amplitude * 0.1) * (_random.NextDouble() * 2 - 1);
                }

                points.Add(new DataPoint { Time = t, Value = value });
            }

            if (settings.TriggerMode != TriggerMode.Roll && points.Count > 0)
            {
                int triggerIndex = -1;
                for (int i = 1; i < points.Count; i++)
                {
                    if (settings.TriggerEdge == TriggerEdge.Rising)
                    {
                        if (points[i - 1].Value < settings.TriggerLevel && points[i].Value >= settings.TriggerLevel)
                        {
                            triggerIndex = i;
                            break;
                        }
                    }
                    else
                    {
                        if (points[i - 1].Value > settings.TriggerLevel && points[i].Value <= settings.TriggerLevel)
                        {
                            triggerIndex = i;
                            break;
                        }
                    }
                }

                if (triggerIndex != -1)
                {
                    var triggeredPoints = points.Skip(triggerIndex).ToList();
                    return triggeredPoints;
                }
            }

            return points;
        }
    }
}
