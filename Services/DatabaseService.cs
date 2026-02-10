using System;
using System.IO;
using Microsoft.Data.Sqlite;
using OscilloscopeApp.Models;

namespace OscilloscopeApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;

        public DatabaseService()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.db");
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Settings (
                            Id INTEGER PRIMARY KEY,
                            WaveType TEXT,
                            Frequency REAL,
                            Amplitude REAL,
                            DutyCycle REAL,
                            NoiseLevel INTEGER,
                            SamplingRate REAL,
                            TimeBase REAL,
                            VoltDiv REAL,
                            TriggerMode TEXT,
                            TriggerEdge TEXT,
                            TriggerLevel REAL
                        );";
                    command.ExecuteNonQuery();
                }
                using (var command2 = connection.CreateCommand())
                {
                    command2.CommandText = @"
                        CREATE TABLE IF NOT EXISTS WaveData (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Time REAL,
                            Value REAL,
                            CreatedAt TEXT
                        );";
                    command2.ExecuteNonQuery();
                }
            }
        }



        public void SaveSettings(SignalSettings settings)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO Settings 
                    (Id, WaveType, Frequency, Amplitude, DutyCycle, NoiseLevel, SamplingRate, TimeBase, VoltDiv, TriggerMode, TriggerEdge, TriggerLevel)
                    VALUES 
                    (1, $waveType, $freq, $amp, $duty, $noise, $sampling, $timeBase, $voltDiv, $trigMode, $trigEdge, $trigLevel)";
                
                command.Parameters.AddWithValue("$waveType", settings.WaveType.ToString());
                command.Parameters.AddWithValue("$freq", settings.Frequency);
                command.Parameters.AddWithValue("$amp", settings.Amplitude);
                command.Parameters.AddWithValue("$duty", settings.DutyCycle);
                command.Parameters.AddWithValue("$noise", settings.NoiseLevel);
                command.Parameters.AddWithValue("$sampling", settings.SamplingRate);
                command.Parameters.AddWithValue("$timeBase", settings.TimeBase);
                command.Parameters.AddWithValue("$voltDiv", settings.VoltDiv);
                command.Parameters.AddWithValue("$trigMode", settings.TriggerMode.ToString());
                command.Parameters.AddWithValue("$trigEdge", settings.TriggerEdge.ToString());
                command.Parameters.AddWithValue("$trigLevel", settings.TriggerLevel);
                
                command.ExecuteNonQuery();
            }
        }

        public SignalSettings LoadSettings()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Settings WHERE Id = 1";
                
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        try
                        {
                            return new SignalSettings
                            {
                                WaveType = Enum.Parse<WaveType>(reader.GetString(1)),
                                Frequency = reader.GetDouble(2),
                                Amplitude = reader.GetDouble(3),
                                DutyCycle = reader.GetDouble(4),
                                NoiseLevel = reader.GetInt32(5),
                                SamplingRate = reader.GetDouble(6),
                                TimeBase = reader.GetDouble(7),
                                VoltDiv = reader.GetDouble(8),
                                TriggerMode = Enum.Parse<TriggerMode>(reader.GetString(9)),
                                TriggerEdge = Enum.Parse<TriggerEdge>(reader.GetString(10)),
                                TriggerLevel = reader.GetDouble(11)
                            };
                        }
                        catch
                        {
                            return new SignalSettings();
                        }
                    }
                }
            }
            return new SignalSettings(); 
        }

        public void SaveWaveData(System.Collections.Generic.IEnumerable<DataPoint> points)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO WaveData (Time, Value, CreatedAt) VALUES ($t, $v, $createdAt)";
                    foreach (var p in points)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("$t", p.Time);
                        command.Parameters.AddWithValue("$v", p.Value);
                        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
        }
    }
}
