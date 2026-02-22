Seraphix API Usage Guide

This guide explains how to use the Seraphix API in a Visual Studio project to interact with Roblox.

API made by filip343filip  
Injector & Modules by vexo6967

Framework:
.NET Framework 4.8
Platform: x64 only

Prerequisites:

- A Visual Studio C# project (WinForms or WPF)
- SeraphixAPI.dll
- .NET Framework 4.8 installed

Steps:

1. Prepare the Project:
   - Open your project in Visual Studio
   - Go to Project Properties
   - Set Platform Target to x64
   - Set Target Framework to .NET Framework 4.8

2. Add Reference:
   - Right-click "References" in Solution Explorer
   - Click "Add Reference"
   - Browse and select SeraphixAPI.dll
   - Click OK

3. Include Namespace:
   - At the top of your C# file, add:
     using SeraphixAPI;

API Functions:

- Inject:
  Api.Inject();

  Injects the API into the Roblox process.

- Execute Script:
  Api.Execute(script);

  Executes a Lua script in Roblox.
  Example: richTextBox1.Text

- Close / Cleanup:
  Api.Closure();

  Cleans up the API.
  Recommended to call when closing your application.

- Custom Injection Notification (Optional):
  Api.SetInjection("Title", "Text");

  Sets a custom notification shown on injection.

- Auto Injector:
  AutoInjector.Enable(true);
  AutoInjector.Enable(checkBox1.Checked);

  Enables or disables automatic injection.
  Can be paired with a checkbox or toggle.

- Kill Roblox:
  Api.KillRoblox();

  Terminates all Roblox processes.

- Check if Roblox is Open:
  bool isOpen = Api.IsRobloxOpen();

  Returns true if Roblox is currently running.

Basic Usage Example:

using SeraphixAPI;

public partial class Form1 : Form
{
public Form1()
{
InitializeComponent();
Api.SetInjection("Title", "Text");
}

    private void InjectButton_Click(object sender, EventArgs e)
    {
        Api.Inject();
    }

    private void ExecuteButton_Click(object sender, EventArgs e)
    {
        Api.Execute(richTextBox1.Text);
    }

    private void AutoInject_CheckedChanged(object sender, EventArgs e)
    {
        AutoInjector.Enable(autoInjectCheckbox.Checked);
    }

    private void KillRobloxButton_Click(object sender, EventArgs e)
    {
        Api.KillRoblox();
    }

    private void Form1_FormClosed(object sender, FormClosedEventArgs e)
    {
        Api.Closure();
    }

}
