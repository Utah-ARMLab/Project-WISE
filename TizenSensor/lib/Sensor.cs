﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Tizen.Sensor;

namespace TizenSensor.lib
{
	/// <summary>
	/// Captures and records readings from the watch's heart rate sensor, accelerometer, and gyroscope.
	/// </summary>
	public class Sensor
	{
		public static void Create(Action<Sensor> onCreated, Action<Sensor, SensorData> onUpdated)
		{
			Permission.Check(
				isAllowed => onCreated(isAllowed ? new Sensor(onUpdated) : null),
				"http://tizen.org/privilege/healthinfo"
			);
		}

		protected Sensor(Action<Sensor, SensorData> onUpdated)
		{
			OnUpdated = onUpdated;
		}

		public Action<Sensor, SensorData> OnUpdated { get; set; }

		public bool IsRunning { get; protected set; } = false;

		protected HeartRateMonitor heartRateMonitor = new HeartRateMonitor
		{
			PausePolicy = SensorPausePolicy.None
		};

		protected Accelerometer accelerometer = new Accelerometer
		{
			PausePolicy = SensorPausePolicy.None
		};

		protected Gyroscope gyroscope = new Gyroscope
		{
			PausePolicy = SensorPausePolicy.None
		};

		protected StreamWriter recordWriter;

		protected Stopwatch stopwatch = new Stopwatch();

		public void Start(string recordFilePath, uint updateInterval)
		{
			if (IsRunning) return;

			recordWriter = new StreamWriter(recordFilePath);
			recordWriter.WriteLine(SensorData.CsvHeader);
			heartRateMonitor.Interval = updateInterval;
			accelerometer.Interval = updateInterval;
			gyroscope.Interval = updateInterval;
			heartRateMonitor.Start();
			accelerometer.Start();
			gyroscope.Start();

			new Thread(() =>
			{
				stopwatch.Start();
				long lastReportTime = -updateInterval; // force immediate report
				while (IsRunning)
				{
					if (stopwatch.ElapsedMilliseconds - lastReportTime < updateInterval) continue;

					lastReportTime += updateInterval;
					Update(lastReportTime);
				}
				stopwatch.Stop();
				stopwatch.Reset();
			}).Start();

			IsRunning = true;
		}

		public void Stop()
		{
			if (!IsRunning) return;

			IsRunning = false;
			recordWriter.Close();
			recordWriter = null;
			heartRateMonitor.Stop();
			accelerometer.Stop();
			gyroscope.Stop();
		}

		protected void Update(long elapsedMilliseconds)
		{
			if (!IsRunning) return;

			var data = new SensorData
			{
				Seconds = elapsedMilliseconds * .001F,
				HeartRate = heartRateMonitor.HeartRate,
				AccelerationX = accelerometer.X,
				AccelerationY = accelerometer.Y,
				AccelerationZ = accelerometer.Z,
				AngularVelocityX = gyroscope.X,
				AngularVelocityY = gyroscope.Y,
				AngularVelocityZ = gyroscope.Z
			};

			recordWriter.WriteLine(data.ToCsvRow());
			OnUpdated.Invoke(this, data);
		}
	}
}
