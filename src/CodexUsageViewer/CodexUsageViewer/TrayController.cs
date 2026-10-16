using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace CodexUsageViewer;

internal sealed class TrayController : IDisposable
{
	private readonly MainWindow window;

	private readonly NotifyIcon notifyIcon;

	private ContextMenuStrip menu;

	private bool disposed;

	public TrayController(MainWindow window)
	{
		this.window = window;
		notifyIcon = new NotifyIcon
		{
			Text = "Codex Usage Viewer",
			Icon = LoadTrayIcon(),
			Visible = true
		};
		NotifyIcon obj = notifyIcon;
		EventHandler value = delegate
		{
			window.Dispatcher.BeginInvoke(new Action(window.ShowFromTray));
		};
		obj.DoubleClick += value;
		window.DisplayChanged += OnDisplayChanged;
		Localization.Changed += OnLanguageChanged;
		RebuildMenu();
		UpdateText();
	}

	private void RebuildMenu()
	{
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add(Localization.Get("TaskTokenUsage"), null, delegate
		{
			window.Dispatcher.BeginInvoke(new Action(window.ShowTaskUsage));
		});
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		ToolStripMenuItem collapse = (ToolStripMenuItem)contextMenuStrip.Items.Add(window.IsCollapsed ? Localization.Get("Expand") : Localization.Get("Collapse"), null, delegate
		{
			window.Dispatcher.BeginInvoke(new Action(window.ToggleCollapsed));
		});
		contextMenuStrip.Items.Add(Localization.Get("Show"), null, delegate
		{
			window.Dispatcher.BeginInvoke(new Action(window.ShowFromTray));
		});
		contextMenuStrip.Items.Add(Localization.Get("HideToTray"), null, delegate
		{
			window.Dispatcher.BeginInvoke(new Action(window.HideTemporarily));
		});
		ToolStripMenuItem topmost = (ToolStripMenuItem)contextMenuStrip.Items.Add(Localization.Get("AlwaysOnTop"), null, delegate
		{
			window.Dispatcher.BeginInvoke(new Action(window.ToggleTopmost));
		});
		ToolStripMenuItem fullScreen = (ToolStripMenuItem)contextMenuStrip.Items.Add(Localization.Get("AutoHideFullscreen"), null, delegate
		{
			window.Dispatcher.BeginInvoke(new Action(window.ToggleAutoHideFullScreen));
		});
		contextMenuStrip.Items.Add(Localization.Get("RefreshNow"), null, delegate
		{
			window.Dispatcher.BeginInvoke((Action)async delegate
			{
				await window.RefreshFromTray();
			});
		});
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(Localization.Get("Language"));
		AddLanguageItem(toolStripMenuItem, Localization.Get("FollowSystem"), "system");
		AddLanguageItem(toolStripMenuItem, Localization.Get("SimplifiedChinese"), "zh-CN");
		AddLanguageItem(toolStripMenuItem, Localization.Get("English"), "en-US");
		contextMenuStrip.Items.Add(toolStripMenuItem);
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(Localization.Get("Exit"), null, delegate
		{
			window.Dispatcher.BeginInvoke(new Action(ExitApplication));
		});
		contextMenuStrip.Opening += delegate
		{
			collapse.Text = (window.IsCollapsed ? Localization.Get("Expand") : Localization.Get("Collapse"));
			topmost.Checked = window.IsAlwaysOnTop;
			fullScreen.Checked = window.IsAutoHideFullScreen;
		};
		ContextMenuStrip contextMenuStrip2 = menu;
		menu = contextMenuStrip;
		notifyIcon.ContextMenuStrip = menu;
		contextMenuStrip2?.Dispose();
		AppLogger.Info("Tray menu rebuilt; language=" + Localization.EffectiveLanguage + "; preference=" + Localization.Preference);
	}

	private void AddLanguageItem(ToolStripMenuItem parent, string text, string value)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text);
		toolStripMenuItem.Checked = Localization.Preference == value;
		ToolStripMenuItem toolStripMenuItem2 = toolStripMenuItem;
		toolStripMenuItem2.Click += delegate
		{
			window.Dispatcher.BeginInvoke((Action)delegate
			{
				window.SetLanguage(value);
			});
		};
		parent.DropDownItems.Add(toolStripMenuItem2);
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			window.DisplayChanged -= OnDisplayChanged;
			Localization.Changed -= OnLanguageChanged;
			notifyIcon.Visible = false;
			notifyIcon.Dispose();
			if (menu != null)
			{
				menu.Dispose();
			}
		}
	}

	private void OnDisplayChanged(object sender, EventArgs e)
	{
		UpdateText();
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		RebuildMenu();
		UpdateText();
	}

	private void UpdateText()
	{
		string trayText = window.TrayText;
		notifyIcon.Text = ((trayText.Length > 63) ? trayText.Substring(0, 63) : trayText);
	}

	private void ExitApplication()
	{
		Dispose();
		window.RequestExit();
		System.Windows.Application.Current.Shutdown();
	}

	private static Icon LoadTrayIcon()
	{
		return Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
	}
}
