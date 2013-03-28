using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using KeePass.Plugins;
using System.Reflection;
using KeePass.Forms;
using KeePassLib.Interfaces;
using System.Collections.Generic;
using KeePass.UI;
using KeePassLib.Serialization;
using KeePassLib.Keys;
using System.Security.Policy;
using KeePassPluginTestUtil.Properties;

namespace KeePassPluginTestUtil
{
  /// <summary>
  /// used to start and stop KeePass for use with testing
  /// </summary>
  public static class KeePassControl
  {
    private const double defaultTimeout = 3000; // msec
    private const string keepassProcName = "KeePass";
    private const string keepassExeName = "KeePass.exe";
    private const string configFileName = "KeePass.config.xml";
    private const string dbFileName = "test{0}.kdbx";
    private const string password = "test";

    /// <summary>
    /// Sends signal for all running instances of KeePass to exit.
    /// </summary>
    public static void ExitAll()
    {
      /* vars */
      string[] args;
      Stopwatch stopwatch;

      // close the instance that we are working with if it exists        
      InvokeMainWindow((MethodInvoker)delegate()
      {
        ToolStripMenuItem FileMenu = (ToolStripMenuItem)KeePass.Program
          .MainForm.MainMenu.Items["m_menuFile"];
        ToolStripMenuItem ExitMenuItem = (ToolStripMenuItem)FileMenu
          .DropDownItems["m_menuFileExit"];
        ExitMenuItem.PerformClick();
        //KeePass.Program.MainForm.Dispose();
      });
      stopwatch = new Stopwatch();
      stopwatch.Start();
      while ((stopwatch.ElapsedMilliseconds < defaultTimeout) &&
          (KeePass.Program.MainForm != null &&
          !KeePass.Program.MainForm.IsDisposed)) {
        Thread.Sleep(250);
      }
      while (KeePass.Program.MainForm != null &&
        !KeePass.Program.MainForm.IsDisposed) {
        ShowErrorMessage(
          "KeePass did not stop within the specified timeout." +
          "\n\n" +
          "Click OK when all KeePass processes have been terminated.");
      }

      /* issue exit all command */
      args = new string[] { "--exit-all" };
      StartKeePassProc(args);

      /* wait for program to close */
      stopwatch.Reset();
      stopwatch.Start();
      while ((stopwatch.ElapsedMilliseconds < defaultTimeout) &&
          (Process.GetProcessesByName(keepassProcName).Length > 0)) {
        Thread.Sleep(250);
      }
      while (Process.GetProcessesByName(keepassProcName).Length > 0) {
        ShowErrorMessage("KeePass did not stop within the specified timeout." +
            "\n\n" +
            "Click OK when all KeePass processes have been terminated.");
      }
    }

    /// <summary>
    /// Stops all running instances of KeePass, then starts a new instance
    /// of KeePass with a barebones database and (mostly) default
    /// configuration.
    /// </summary>
    /// <returns>IPluginHost object from KeePass</returns>
    [Obsolete]
    public static IPluginHost StartKeePass()
    {
      return StartKeePass(true);
    }

    /// <summary>
    /// Stats second instance of KeePass
    /// </summary>
    [Obsolete]
    public static IPluginHost StartSecondKeePass()
    {
      return StartKeePass(false, false, 1, defaultTimeout);
    }

    /// <summary>
    /// Starts a new instance of KeePass with a barebones database and
    /// (mostly) default configuration.
    /// </summary>
    /// <param name="exitAllFirst">If set to true, the ExitAll() method
    /// will be called first to close any running instances of KeePass
    /// </param>
    /// <returns>IPluginHost object from KeePass</returns>
    [Obsolete]
    public static IPluginHost StartKeePass(bool exitAllFirst)
    {
      return StartKeePass(exitAllFirst, true, 1, defaultTimeout);
    }

    [Obsolete]
    public static IPluginHost StartKeePass(bool exitAllFirst,
        bool copyConfig, int numDbFiles)
    {
      return StartKeePass(exitAllFirst, copyConfig, numDbFiles,
          defaultTimeout);
    }

    /// <summary>
    /// Starts a new instance of KeePass with a barebones database and
    /// (mostly) default configuration.
    /// </summary>
    /// <param name="exitAllFirst">If set to true, the ExitAll() method
    /// will be called first to close any running instances of KeePass
    /// </param>
    /// <param name="timeout">The time to wait in milliseconds for KeePass
    /// to start before showing error message.
    /// Also applies to waiting for ExitAll if exitAllFirst is true.
    /// </param>
    /// <returns>IPluginHost object from KeePass</returns>
    [Obsolete]
    public static IPluginHost StartKeePass(bool exitAllFirst,
        bool copyConfig, int numDbFiles, double timeout)
    {
      if (KeePass.Program.MainForm != null) {
        ShowErrorMessage("Can only run KeePass once per test (AppDomain)");
        return null;
      }

      if (numDbFiles < 1) {
        throw new ArgumentOutOfRangeException("numDbFiles");
      }

      /* vars */
      string assmDir;
      string configFile;
      List<string> testDbFiles;
      string keepassExeFile;
      string[] args;
      Assembly assembly;
      Stopwatch stopwatch;
      DialogResult result;

      if (exitAllFirst) {
        ExitAll(); // close any open instances of keepass
      }

      /* verify directories */

      assembly = Assembly.GetAssembly(typeof(KeePass.Program));
      assmDir = Path.GetDirectoryName(assembly.Location);
      // really shouldn't need to check this
      if (!Directory.Exists(assmDir)) {
        ShowErrorMessage("Directory '" + assmDir + "' does not exist." +
            "\nIt should be the location of " + keepassExeName);
        return null;
      }

      /* verify files */

      keepassExeFile = Path.Combine(assmDir, keepassExeName);
      if (!File.Exists(keepassExeFile)) {
        ShowErrorMessage("KeePass executable file '" + keepassExeFile +
          "' does not exist." +
          "\nPlease make sure it is set up in References and 'Copy Local' "+
          "property is set to true" +
          "\nor fix 'keepassExe' in 'KeePassControl.cs'");
        return null;
      }

      /* copy files to working directory */
      if (copyConfig) {
        configFile = Path.Combine(assmDir, configFileName);
        try {
          File.WriteAllText(configFile, Resources.KeePass_config_xml);
        } catch (Exception ex) {
          ShowErrorMessage("Error writing config file '" + configFile + "'." +
              "\n\n" + ex.Message);
          return null;
        }
      }

      testDbFiles = new List<string>();
      for (int i = 1; i <= numDbFiles; i++) {
        string testDbFileN = Path.Combine(assmDir,
          string.Format(dbFileName, i));
        try {
          File.WriteAllBytes(testDbFileN, Resources.test_kdbx);
          testDbFiles.Add(testDbFileN);
        } catch (Exception ex) {
          ShowErrorMessage("Error writing database file '" + testDbFileN +
            "'." + "\n\n" + ex.Message);
          return null;
        }
      }

      /* start keepass with test1.kdbx db */
      try {
        args = new string[] { 
                    testDbFiles[0],
                    "-pw:" + password,
                    "--debug"
                };

        StartKeePassProc(args);

        /* attach local KeePass instance to the process that we just started */
        ThreadStart startKeePass = new ThreadStart(delegate()
        {
          KeePass.Program.Main(null);
        });
        Thread kpThread = new Thread(startKeePass);
        kpThread.SetApartmentState(ApartmentState.STA);
        kpThread.Start();

      } catch (Exception ex) {
        ShowErrorMessage("An exception occurred while starting KeePass" +
            "\n\n" + ex.ToString());
        return null;
      }

      /* wait for KeyPass to open */
      stopwatch = new Stopwatch();
      stopwatch.Start();
      while ((stopwatch.ElapsedMilliseconds < timeout) &&
          ((KeePass.Program.MainForm == null) ||
          (KeePass.Program.MainForm.PluginHost == null))) {

        Thread.Sleep(250);
      }
      /* wait for file to open if we asked for at least one file */
      if (numDbFiles >= 1) {
        while ((stopwatch.ElapsedMilliseconds < timeout) &&
            !KeePass.Program.MainForm.IsAtLeastOneFileOpen()) {

          Thread.Sleep(250);
        }
      }
      stopwatch.Stop();

      /* verify that program started */
      while (KeePass.Program.MainForm == null) {
        result = ShowErrorMessage(
          "KeePass did not start within the specified timeout." +
          "\n\nClick OK when KeyPass has started.", true);
        if (result == DialogResult.Cancel) {
          return null;
        }
      }

      /* open additional database files */
      if (testDbFiles.Count > 1) {
        for (int i = 1, cnt = testDbFiles.Count; i < cnt; i++) {
          try {
            IOConnectionInfo ioConnection = new IOConnectionInfo();
            ioConnection.Path = testDbFiles[i];
            CompositeKey compositeKey = new CompositeKey();
            KcpPassword passwordKey = new KcpPassword(password);
            compositeKey.AddUserKey(passwordKey);
            MethodInvoker methodInvoker = new MethodInvoker(delegate()
            {
              KeePass.Program.MainForm.OpenDatabase(ioConnection,
                compositeKey, true);
            });
            InvokeMainWindow(methodInvoker);
          } catch (Exception ex) {
            ShowErrorMessage(
              "An exception occured while opening additional database file" +
              "\n\n" + ex.Message);
            return null;
          }
        }
      }

      while (KeePass.Program.MainForm.PluginHost == null) {
        result = ShowErrorMessage("Cannot get PluginHost object." +
          " Make sure a file is open in KeePass." +
            "\n\nClick OK when file is open.", true);
        if (result == DialogResult.Cancel) {
          return null;
        }
      }

      // plugins are disabled in config file so that none are loaded 
      // automatically re-enable now so that we can get to the plugin dialog
      KeePass.App.AppPolicy.Current.Plugins = true;

      return KeePass.Program.MainForm.PluginHost;
    }


    public static void CreatePlgx(PlgxBuildOptions options)
    {
      List<string> args = new List<string>();
      args.Add("--plgx-create");
      if (options.projectPath != null) {
        args.Add(options.projectPath);
      }
      if (options.keepassVersion != null) {
        args.Add("--plgx-prereq-kp:" + options.keepassVersion);
      }
      if (options.dotnetVersion != null) {
        args.Add("--plgx-prereq-net:" + options.dotnetVersion);
      }
      if (options.os != null) {
        args.Add("--plgx-prereq-os:" + options.os);
      }
      if (options.pointerSize != null) {
        args.Add("--plgx-prereq-ptr:" + options.pointerSize);
      }
      if (options.preBuild != null) {
        args.Add("--plgx-build-pre:" + options.preBuild);
      }
      if (options.postBuild != null) {
        args.Add("--plgx-build-post:" + options.postBuild);
      }
      Process keePassProc = StartKeePassProc(args.ToArray());

      // TODO May want a time out here
      while (!keePassProc.HasExited) {
        Thread.Sleep(250);
      }
    }

    public static void LoadPlgx(string plgxPath)
    {
      MethodInvoker methodInvoker = new MethodInvoker(delegate()
      {
        OnDemandStatusDialog dlgStatus = new OnDemandStatusDialog(true, null);
        dlgStatus.StartLogging(plgxPath, false);

        KeePass.Plugins.PlgxPlugin.Load(plgxPath, dlgStatus);

        dlgStatus.EndLogging();
      });

      InvokeMainWindow(methodInvoker);
    }

    /* convience methods */

    /// <summary>
    /// Helper method for showing Error MessageBox with OK button
    /// </summary>
    /// <param name="message">Message to show</param>
    /// <returns>DialogResult from MessageBox</returns>
    internal static DialogResult ShowErrorMessage(string message)
    {
      return ShowErrorMessage(message, false);
    }

    /// <summary>
    /// Helper method for showing Error MessageBox
    /// </summary>
    /// <param name="message">Message to show</param>
    /// <param name="cancelable">If true, shows OK and Cancel button.
    /// If false, shows OK button only</param>
    /// <returns>DialogResult from MessageBox</returns>
    private static DialogResult ShowErrorMessage(string message,
      bool cancelable)
    {
      MessageBoxButtons buttons;
      if (cancelable) {
        buttons = MessageBoxButtons.OKCancel;
      } else {
        buttons = MessageBoxButtons.OK;
      }
      return MessageBox.Show(message, "Error", buttons, MessageBoxIcon.Error);
    }

    /// <summary>
    /// Starts keypass in a separate process
    /// </summary>
    /// <param name="args">argument to pass</param>
    /// <returns>the process that was started</returns>
    private static Process StartKeePassProc(string[] args)
    {
      Assembly assembly = Assembly.GetAssembly(typeof(KeePass.Program));
      string assmDir = Path.GetDirectoryName(assembly.Location);
      string keepassExeFile = Path.Combine(assmDir, keepassExeName);

      Process keepassProc = new Process();
      keepassProc.StartInfo.FileName = keepassExeFile;
      keepassProc.StartInfo.Arguments = string.Join(" ", args);
      keepassProc.Start();

      return keepassProc;
    }

    public static void InvokeMainWindow(MethodInvoker methodInvoker)
    {
      Form mainWindow = KeePass.Program.MainForm;
      if (mainWindow != null && !mainWindow.IsDisposed) {
        if (mainWindow.InvokeRequired) {
          mainWindow.Invoke(methodInvoker);
        } else {
          methodInvoker.Invoke();
        }
      }
    }

  }
}
