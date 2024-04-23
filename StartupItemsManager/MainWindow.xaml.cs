// Copyright (c) 2024 David A. Frischknecht
//
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Visibility = System.Windows.Visibility;

namespace StartupItemsManager;

public partial class MainWindow
{
	private static readonly DependencyPropertyKey s_shieldImagePropertyKey =
		DependencyProperty.RegisterReadOnly(nameof(ShieldImage), typeof(BitmapSource), typeof(MainWindow), new());

	public static readonly DependencyProperty ShieldImageProperty = s_shieldImagePropertyKey.DependencyProperty;

	private static readonly DependencyPropertyKey s_regCurrentUserRunItemsPropertyKey =
		DependencyProperty.RegisterReadOnly(nameof(RegCurrentUserRunItems),
			typeof(ObservableCollection<RegStartupItem>), typeof(MainWindow), new());

	public static readonly DependencyProperty RegCurrentUserRunItemsProperty =
		s_regCurrentUserRunItemsPropertyKey.DependencyProperty;

	private static readonly DependencyPropertyKey s_startMenuCurrentUserStartupItemsPropertyKey =
		DependencyProperty.RegisterReadOnly(nameof(StartMenuCurrentUserStartupItems),
			typeof(ObservableCollection<StartMenuStartupItem>), typeof(MainWindow), new());

	public static readonly DependencyProperty StartMenuCurrentUserStartupItemsProperty =
		s_startMenuCurrentUserStartupItemsPropertyKey.DependencyProperty;

	private const int maxPath = 260;
	private const uint idShield = 77;
	private const uint flagIcon = 0x000000100;
	private const uint flagSmallIcon = 0x000000001;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct StockIconInfo
	{
		public UInt32 cbSize;
		public IntPtr hIcon;
		public Int32 iSysIconIndex;
		public Int32 iIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = maxPath)]
		public string szPath;
	}

	[DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref StockIconInfo stockIconInfo);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DestroyIcon(IntPtr hIcon);

	public MainWindow()
	{
		InitializeComponent();
		var stockIconInfo = new StockIconInfo();
		stockIconInfo.cbSize = (uint)Marshal.SizeOf(stockIconInfo);
		_ = SHGetStockIconInfo(idShield, flagIcon | flagSmallIcon, ref stockIconInfo);
		ShieldImage = Imaging.CreateBitmapSourceFromHIcon(stockIconInfo.hIcon, Int32Rect.Empty,
			BitmapSizeOptions.FromEmptyOptions());
		DestroyIcon(stockIconInfo.hIcon);
		RegCurrentUserRunItems = [];
		RegCurrentUserRunItems.CollectionChanged += RegCurrentUserRunItems_CollectionChanged;

		StartMenuCurrentUserStartupItems = [];
		StartMenuCurrentUserStartupItems.CollectionChanged += this.StartMenuCurrentUserStartupItems_CollectionChanged;
	}

	private void StartMenuCurrentUserStartupItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
			{
				if (e.NewItems == null) return;
				foreach (var newItem in e.NewItems)
				{
					var startupItem = (StartMenuStartupItem)newItem;
					if (startupItem == null) continue;
					startupItem.PropertyChanged += StartMenuCurrentUserStartupItem_PropertyChanged;
				}
				break;
			}

			case NotifyCollectionChangedAction.Remove:
			{
				var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
				if (e.OldItems == null) return;
				foreach (var oldItem in e.OldItems)
				{
					var startupItem = (StartMenuStartupItem)oldItem;
					if (startupItem == null) continue;
					File.Delete(Path.Combine(folder, startupItem.Name));
					startupItem.PropertyChanged -= StartMenuCurrentUserStartupItem_PropertyChanged;
				}
				break;
			}

			case NotifyCollectionChangedAction.Replace:
			case NotifyCollectionChangedAction.Move:
			case NotifyCollectionChangedAction.Reset:
			default:
				break;
		}
	}

	private void StartMenuCurrentUserStartupItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
		var existingFiles = Directory.EnumerateFiles(folder);
		foreach (var existingFile in existingFiles)
		{
			File.Delete(existingFile);
		}

		foreach (var startupItem in StartMenuCurrentUserStartupItems)
		{
			ShellLink.Create(Path.Combine(folder, startupItem.Name), new ShellItem(startupItem.Target), null, null, startupItem.Arguments);
		}
	}

	// ReSharper disable once MemberCanBeMadeStatic.Local
	private void RegCurrentUserRunItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		using var regKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
			{
				if (e.NewItems == null) return;
				foreach (var newItem in e.NewItems)
				{
					var startupItem = (RegStartupItem)newItem;
					if (startupItem == null) continue;
					startupItem.PropertyChanged += RegCurrentUserStartupItem_PropertyChanged;
				}
				break;
			}

			case NotifyCollectionChangedAction.Remove:
			{
				if (e.OldItems == null) return;
				foreach (var oldItem in e.OldItems)
				{
					var startupItem = (RegStartupItem)oldItem;
					if (startupItem == null) continue;
					regKey.DeleteValue(startupItem.Name);
					startupItem.PropertyChanged -= RegCurrentUserStartupItem_PropertyChanged;
				}
				break;
			}

			case NotifyCollectionChangedAction.Move:
			case NotifyCollectionChangedAction.Replace:
			case NotifyCollectionChangedAction.Reset:
			default:
				break;
		}
	}

	private void RegCurrentUserStartupItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		using var regKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

		var valueNames = regKey.GetValueNames();
		foreach (var valueName in valueNames)
		{
			regKey.DeleteValue(valueName);
		}

		foreach (var regCurrentUserRunItem in RegCurrentUserRunItems)
		{
			regKey.SetValue(regCurrentUserRunItem.Name, regCurrentUserRunItem.CommandLine);
		}
	}

	public ObservableCollection<StartMenuStartupItem> StartMenuCurrentUserStartupItems
	{
		get => (ObservableCollection<StartMenuStartupItem>)GetValue(StartMenuCurrentUserStartupItemsProperty);
		private init => SetValue(s_startMenuCurrentUserStartupItemsPropertyKey, value);
	}

	public ObservableCollection<RegStartupItem> RegCurrentUserRunItems
	{
		get => (ObservableCollection<RegStartupItem>)GetValue(RegCurrentUserRunItemsProperty);
		private init => SetValue(s_regCurrentUserRunItemsPropertyKey, value);
	}

	public BitmapSource ShieldImage
	{
		get => (BitmapSource)GetValue(s_shieldImagePropertyKey.DependencyProperty);
		private init => SetValue(s_shieldImagePropertyKey, value);
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		PopulateRegCurrentUserRun();
		PopulateStartMenuCurrentUserRun();

		if (!((App.Current as App)!).IsElevated) return;
		m_grdLaunchAsAdmin.Visibility = Visibility.Collapsed;
		Title = $"Administrator: {Title}";
	}

	private void BtnLaunchAsAdmin_Click(object sender, RoutedEventArgs e)
	{
		var assemblyPath = Assembly.GetExecutingAssembly().Location.Replace("dll", "exe", StringComparison.OrdinalIgnoreCase);
		var startInfo = new ProcessStartInfo()
		{
			FileName = assemblyPath,
			Verb = "runas",
			UseShellExecute = true
		};

		try
		{
			var process = Process.Start(startInfo);
			if (process != null)
			{
				Close();
			}
		}
		catch
		{
			// ignored
		}
	}

	private void PopulateStartMenuCurrentUserRun()
	{
		var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
		var existingFiles = Directory.EnumerateFiles(folder, "*.*", new EnumerationOptions()
		{
			IgnoreInaccessible = true
		});
		foreach (var file in existingFiles)
		{
			var link = new ShellLink(file, LinkResolution.NoUI, new WindowInteropHelper(this).EnsureHandle());
			StartMenuCurrentUserStartupItems.Add(new() { Name = link.Name!, Target = link.TargetPath, Arguments = link.Arguments });
		}
	}

	private void PopulateRegCurrentUserRun()
	{
		using var regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
		if (regKey == null) return;

		RegCurrentUserRunItems.Clear();

		var valueNames = regKey.GetValueNames();

		foreach (var valueName in valueNames)
		{
			if (string.IsNullOrWhiteSpace(valueName)) continue;
			var commandLine = regKey.GetValue(valueName);
			RegCurrentUserRunItems.Add(new() { Name = valueName, CommandLine = commandLine!.ToString()! });
		}
	}

	public sealed class StartMenuStartupItem : INotifyPropertyChanged
	{
		private string m_name = "";
		private string m_target = "";
		private string m_arguments = "";

		public string Name
		{
			get => m_name;
			set
			{
				if (m_name == value) return;
				_ = SetField(ref m_name, value);
			}
		}

		public string Target
		{
			get => m_target;
			set
			{
				if (m_target == value) return;
				_ = SetField(ref m_target, value);
			}
		}

		public string Arguments
		{
			get => m_arguments;
			set
			{
				if (m_arguments == value) return;
				_ = SetField(ref m_arguments, value);
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new(propertyName));
		}

		private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}
	}

	public sealed class RegStartupItem : INotifyPropertyChanged
	{
		private string m_name = "";
		private string m_commandLine = "";

		public string Name
		{
			get => m_name;
			// ReSharper disable once PropertyCanBeMadeInitOnly.Global
			set
			{
				if (m_name == value) return;
				_ = SetField(ref m_name, value);
			}
		}

		public string CommandLine
		{
			get => m_commandLine;
			// ReSharper disable once PropertyCanBeMadeInitOnly.Global
			set
			{
				if (m_commandLine == value) return;
				_ = SetField(ref m_commandLine, value);
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new(propertyName));
		}

		private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}
	}

	private void DgRegCurrentUserRun_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
	{
		if (e.PropertyName == nameof(RegStartupItem.CommandLine))
		{
			e.Column.Header = "Command Line";
		}
	}
}