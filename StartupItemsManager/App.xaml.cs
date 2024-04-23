// Copyright (c) 2024 David A. Frischknecht
//
// SPDX-License-Identifier: Apache-2.0

using System.Security.Principal;
using System.Windows;

namespace StartupItemsManager;

public partial class App
{
	public bool IsElevated { get; private set; }

	private void App_OnStartup(object sender, StartupEventArgs e)
	{
		var identity = WindowsIdentity.GetCurrent();
		var principal = new WindowsPrincipal(identity);
		IsElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
	}
}
