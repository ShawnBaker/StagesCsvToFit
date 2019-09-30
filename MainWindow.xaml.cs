// Copyright © 2019 Shawn Baker using the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Dynastream.Fit;

namespace StagesCsvToFit
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		// instance variables
		private OpenFileDialog openFileDialog;
		private LapsList laps;

		/// <summary>
		/// Constructor
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();

			// create the open file dialog
			openFileDialog = new OpenFileDialog();
			openFileDialog.FileName = "";
			openFileDialog.DefaultExt = "csv";
			openFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
			openFileDialog.FilterIndex = 1;
		}

		/// <summary>
		/// Opens a FIT file.
		/// </summary>
		private void OpenFileButton_Click(object sender, RoutedEventArgs e)
		{
			// set the initial directory from Settings.OpenFilePath
			string path = (string)Properties.Settings.Default["OpenFilePath"];
			if (path.Length == 0)
			{
				path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			}
			openFileDialog.InitialDirectory = path;

			// prompt the user for a file name
			bool? result = openFileDialog.ShowDialog(this);
			if (result == true)
			{
				// update Settings.OpenFilePath from the file name
				path = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
				Properties.Settings.Default["OpenFilePath"] = path;
				Properties.Settings.Default.Save();
				string fileName = openFileDialog.FileName;

				// get the CSV and FIT file names
				string csvFileName = openFileDialog.FileName;
				string fitFileName = Path.ChangeExtension(csvFileName, "fit");
				FileNameTextBox.Text = csvFileName;

				// get the start time
				string[] parts = Path.GetFileNameWithoutExtension(csvFileName).Split(new char[] { '-' });
				System.DateTime start = System.IO.File.GetCreationTime(csvFileName);
				if (parts.Length == 5)
				{
					try
					{
						start = new System.DateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]), 0, DateTimeKind.Local);
					}
					catch { }
				}

				// read the lines from the CSV file
				laps = new LapsList();
				Lap lap = new Lap();
				laps.Add(lap);
				string[] textLines = System.IO.File.ReadAllLines(csvFileName);

				// convert the CSV lines into FIT records and laps
				for (int i = 0; i < textLines.Length; i++)
				{
					string line = textLines[i];
					if (!Char.IsDigit(line[0]))
					{
						if (line.StartsWith("Stage_"))
						{
							lap = new Lap();
							laps.Add(lap);
						}
						continue;
					}
					Record record = new Record(line, start);
					if (record.Speed != 0 || record.Power != 0 || record.HeartRate != 0 || record.Cadence != 0)
					{
						lap.Records.Add(record);
					}
				}

				// remove trailing rundown records from the last lap
				Lap firstLap = laps[0];
				Record firstRecord = firstLap.Records[0];
				Lap lastLap = laps[laps.Count - 1];
				Record lastRecord = lastLap.Records[lastLap.Records.Count - 1];
				Summary lapSummary = GetLapSummary(lastLap);
				double speedThreshold = lapSummary.AveSpeed * 0.6;
				int j = lastLap.Records.Count - 1;
				while (j >= 0 && lastLap.Records[j].Speed < speedThreshold)
				{
					j--;
				}
				if (j != lastLap.Records.Count - 1)
				{
					lastLap.Records.RemoveRange(j + 1, lastLap.Records.Count - j - 1);
				}

				// display a summary of the file
				Summary sessionSummary = GetSessionSummary(laps);
				NumRecordsLabel.Content = sessionSummary.NumRecords.ToString();
				NumLapsLabel.Content = laps.Count.ToString();
				System.DateTime startTime = firstRecord.Time.ToLocalTime();
				StartDateLabel.Content = startTime.ToString("yyyy/MM/dd");
				StartTimeLabel.Content = startTime.ToString("HH:mm:ss");
				ElapsedTimeLabel.Content = (lastRecord.Time - firstRecord.Time).ToString();
				AveCadenceLabel.Content = sessionSummary.AveCadence.ToString("0");
				MaxCadenceLabel.Content = sessionSummary.MaxCadence.ToString();
				AvePowerLabel.Content = sessionSummary.AvePower.ToString("0");
				MaxPowerLabel.Content = sessionSummary.MaxPower.ToString();
				AveHeartRateLabel.Content = sessionSummary.AveHeartRate.ToString("0");
				MaxHeartRateLabel.Content = sessionSummary.MaxHeartRate.ToString();
				AveSpeedLabel.Content = sessionSummary.AveSpeed.ToString("0.##");
				MaxSpeedLabel.Content = sessionSummary.MaxSpeed.ToString("0.##");

				// write the data to a FIT file
				EncodeActivityFile(fitFileName, start, laps);
			}
		}

		/// <summary>
		/// Displays the about window.
		/// </summary>
		private void AboutButton_Click(object sender, RoutedEventArgs e)
		{
			AboutWindow window = new AboutWindow();
			window.Owner = this;
			window.ShowDialog();
		}

		/// <summary>
		/// Writes the data to a FIT file.
		/// </summary>
		/// <param name="fileName">Name of the FIT file to write to.</param>
		/// <param name="start">Start date/time of the activity.</param>
		/// <param name="laps">Lap and record data to be written.</param>
		static void EncodeActivityFile(string fileName, System.DateTime start, LapsList laps)
		{
			// open the encoder and stream
			Encode encoder = new Encode(ProtocolVersion.V20);
			FileStream fitStream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
			encoder.Open(fitStream);

			// write the file ID message
			FileIdMesg fileIdMsg = new FileIdMesg();
			fileIdMsg.SetType(Dynastream.Fit.File.Activity);
			fileIdMsg.SetManufacturer(Manufacturer.Dynastream);
			fileIdMsg.SetProduct(22);
			fileIdMsg.SetSerialNumber(1234);
			fileIdMsg.SetTimeCreated(new Dynastream.Fit.DateTime(start));
			encoder.Write(fileIdMsg);
			/*
			// write the device ID message
			DeviceInfoMesg deviceInfoMsg = new DeviceInfoMesg();
			deviceInfoMsg.SetTimestamp(new Dynastream.Fit.DateTime(start));
			deviceInfoMsg.SetManufacturer(Manufacturer.StagesCycling);
			deviceInfoMsg.SetProduct(22);
			deviceInfoMsg.SetSerialNumber(1234);
			//deviceInfoMsg.SetDeviceType();
			encoder.Write(deviceInfoMsg);
			*/
			// write the record and lap messages
			foreach (Lap lap in laps)
			{
				// write the record messages
				foreach (Record record in lap.Records)
				{
					RecordMesg recordMsg = new RecordMesg();
					recordMsg.SetTimestamp(new Dynastream.Fit.DateTime(record.Time.ToUniversalTime()));
					recordMsg.SetHeartRate((byte)record.HeartRate);
					recordMsg.SetCadence((byte)record.Cadence);
					recordMsg.SetDistance((float)record.Distance * 1000);
					recordMsg.SetSpeed((float)(record.Speed / 3.6));
					recordMsg.SetPower((ushort)record.Power);
					encoder.Write(recordMsg);
				}

				// write the lap message
				Record first = lap.Records[0];
				Record last = lap.Records[lap.Records.Count - 1];
				TimeSpan time = last.Time - first.Time;
				LapMesg lapMsg = new LapMesg();
				lapMsg.SetTimestamp(new Dynastream.Fit.DateTime(last.Time.ToUniversalTime()));
				lapMsg.SetStartTime(new Dynastream.Fit.DateTime(first.Time.ToUniversalTime()));
				lapMsg.SetTotalElapsedTime((int)time.TotalSeconds);
				lapMsg.SetTotalTimerTime((int)time.TotalSeconds);
				lapMsg.SetTotalDistance((float)(last.Distance - first.Distance) * 1000);
				lapMsg.SetEvent(Event.Lap);
				lapMsg.SetEventType(EventType.Stop);
				lapMsg.SetIntensity(Intensity.Active);
				lapMsg.SetLapTrigger(LapTrigger.Manual);
				lapMsg.SetSport(Sport.Cycling);
				Summary lapSummary = GetLapSummary(lap);
				lapMsg.SetAvgCadence((byte)lapSummary.AveCadence);
				lapMsg.SetMaxCadence((byte)lapSummary.MaxCadence);
				lapMsg.SetAvgHeartRate((byte)lapSummary.AveHeartRate);
				lapMsg.SetMaxHeartRate((byte)lapSummary.MaxHeartRate);
				lapMsg.SetAvgPower((ushort)lapSummary.AvePower);
				lapMsg.SetMaxPower((ushort)lapSummary.MaxPower);
				lapMsg.SetAvgSpeed((float)lapSummary.AveSpeed / 3.6f);
				lapMsg.SetMaxSpeed((float)lapSummary.MaxSpeed / 3.6f);
				encoder.Write(lapMsg);
			}

			// get the first and last laps and records
			Lap firstLap = laps[0];
			Record firstRecord = firstLap.Records[0];
			Lap lastLap = laps[laps.Count - 1];
			Record lastRecord = lastLap.Records[lastLap.Records.Count - 1];
			TimeSpan totalTime = lastRecord.Time - firstRecord.Time;

			// write the session message
			SessionMesg sessionMsg = new SessionMesg();
			sessionMsg.SetTimestamp(new Dynastream.Fit.DateTime(lastRecord.Time.ToUniversalTime()));
			sessionMsg.SetStartTime(new Dynastream.Fit.DateTime(firstRecord.Time.ToUniversalTime()));
			sessionMsg.SetTotalElapsedTime((int)totalTime.TotalSeconds);
			sessionMsg.SetTotalTimerTime((int)totalTime.TotalSeconds);
			sessionMsg.SetTotalDistance((float)(lastRecord.Distance - firstRecord.Distance) * 1000);
			sessionMsg.SetFirstLapIndex(0);
			sessionMsg.SetNumLaps((ushort)laps.Count);
			sessionMsg.SetEvent(Event.Session);
			sessionMsg.SetEventType(EventType.Stop);
			sessionMsg.SetSport(Sport.Cycling);
			sessionMsg.SetSubSport(SubSport.Spin);
			Summary sessionSummary = GetSessionSummary(laps);
			sessionMsg.SetAvgCadence((byte)sessionSummary.AveCadence);
			sessionMsg.SetMaxCadence((byte)sessionSummary.MaxCadence);
			sessionMsg.SetAvgHeartRate((byte)sessionSummary.AveHeartRate);
			sessionMsg.SetMaxHeartRate((byte)sessionSummary.MaxHeartRate);
			sessionMsg.SetAvgPower((ushort)sessionSummary.AvePower);
			sessionMsg.SetMaxPower((ushort)sessionSummary.MaxPower);
			sessionMsg.SetAvgSpeed((float)sessionSummary.AveSpeed / 3.6f);
			sessionMsg.SetMaxSpeed((float)sessionSummary.MaxSpeed / 3.6f);
			encoder.Write(sessionMsg);

			// write the activity message
			ActivityMesg activityMsg = new ActivityMesg();
			activityMsg.SetTimestamp(new Dynastream.Fit.DateTime(lastRecord.Time.ToUniversalTime()));
			activityMsg.SetTotalTimerTime((int)totalTime.TotalSeconds);
			activityMsg.SetNumSessions(1);
			activityMsg.SetType(Activity.Manual);
			activityMsg.SetEvent(Event.Activity);
			activityMsg.SetEventType(EventType.Stop);
			encoder.Write(activityMsg);

			// close the encoder and stream
			encoder.Close();
			fitStream.Close();
		}

		/// <summary>
		/// Gets a summary of the data for a single lap.
		/// </summary>
		/// <param name="lap">Lap to get the summary for.</param>
		/// <returns>The new summary.</returns>
		private static Summary GetLapSummary(Lap lap)
		{
			Summary summary = new Summary();
			foreach (Record record in lap.Records)
			{
				summary.AveSpeed += record.Speed;
				summary.AvePower += record.Power;
				summary.AveHeartRate += record.HeartRate;
				summary.AveCadence += record.Cadence;
				if (record.Speed > summary.MaxSpeed)
				{
					summary.MaxSpeed = record.Speed;
				}
				if (record.Power > summary.MaxPower)
				{
					summary.MaxPower = record.Power;
				}
				if (record.HeartRate > summary.MaxHeartRate)
				{
					summary.MaxHeartRate = record.HeartRate;
				}
				if (record.Cadence > summary.MaxCadence)
				{
					summary.MaxCadence = record.Cadence;
				}
			}
			summary.NumRecords = lap.Records.Count;
			if (summary.NumRecords != 0)
			{
				summary.AveSpeed = summary.AveSpeed / summary.NumRecords;
				summary.AvePower = summary.AvePower / summary.NumRecords;
				summary.AveHeartRate = summary.AveHeartRate / summary.NumRecords;
				summary.AveCadence = summary.AveCadence / summary.NumRecords;
			}
			return summary;
		}

		/// <summary>
		/// Gets a summary of the data for all the laps.
		/// </summary>
		/// <param name="lap">List of laps to get the summary for.</param>
		/// <returns>The new summary.</returns>
		private static Summary GetSessionSummary(LapsList laps)
		{
			Summary summary = new Summary();
			foreach (Lap lap in laps)
			{
				foreach (Record record in lap.Records)
				{
					summary.AveSpeed += record.Speed;
					summary.AvePower += record.Power;
					summary.AveHeartRate += record.HeartRate;
					summary.AveCadence += record.Cadence;
					if (record.Speed > summary.MaxSpeed)
					{
						summary.MaxSpeed = record.Speed;
					}
					if (record.Power > summary.MaxPower)
					{
						summary.MaxPower = record.Power;
					}
					if (record.HeartRate > summary.MaxHeartRate)
					{
						summary.MaxHeartRate = record.HeartRate;
					}
					if (record.Cadence > summary.MaxCadence)
					{
						summary.MaxCadence = record.Cadence;
					}
				}
				summary.NumRecords += lap.Records.Count;
			}
			if (summary.NumRecords != 0)
			{
				summary.AveSpeed = summary.AveSpeed / summary.NumRecords;
				summary.AvePower = summary.AvePower / summary.NumRecords;
				summary.AveHeartRate = summary.AveHeartRate / summary.NumRecords;
				summary.AveCadence = summary.AveCadence / summary.NumRecords;
			}
			return summary;
		}

		/// <summary>
		/// Stores the data for a single record.
		/// </summary>
		private class Record
		{
			public System.DateTime Time;
			public double Distance;
			public double Speed;
			public int Power;
			public int HeartRate;
			public int Cadence;

			public Record(string line, System.DateTime start)
			{
				string[] parts = line.Split(new char[] { ',' });
				string[] timeParts = parts[0].Split(new char[] { ':' });
				if (timeParts.Length == 2)
				{
					Time = start + new TimeSpan(0, int.Parse(timeParts[0]), int.Parse(timeParts[1]));
				}
				else
				{
					Time = start + new TimeSpan(int.Parse(timeParts[0]), int.Parse(timeParts[1]), int.Parse(timeParts[2]));
				}
				Distance = double.Parse(parts[1]);
				Speed = double.Parse(parts[2]);
				Power = int.Parse(parts[3]);
				HeartRate = int.Parse(parts[4]);
				Cadence = int.Parse(parts[5]);
			}
		}

		private class RecordsList : List<Record> { }

		/// <summary>
		/// Stores the data for a single lap.
		/// </summary>
		private class Lap
		{
			public RecordsList Records = new RecordsList();
		}

		private class LapsList : List<Lap> { }

		/// <summary>
		/// A summary of a group of records.
		/// </summary>
		private class Summary
		{
			public int NumRecords = 0;
			public double AveSpeed = 0;
			public double AvePower = 0;
			public double AveHeartRate = 0;
			public double AveCadence = 0;
			public double MaxSpeed = 0;
			public int MaxPower = 0;
			public int MaxHeartRate = 0;
			public int MaxCadence = 0;
		}
	}
}
