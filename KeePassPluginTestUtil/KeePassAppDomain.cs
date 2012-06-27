using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Reflection;
using System.Threading;
using System.IO;
using KeePassLib.Serialization;
using KeePassLib.Keys;

namespace KeePassPluginTestUtil
{
    /// <summary>
    /// Uses <see cref="System.AppDomain"/> to run multiple KeePass instances
    /// in tests.
    /// <remarks>Since KeePass.Program is a static class, KeePass can only
    /// be run once in a test suite. To work around that problem, you can use
    /// an <see cref="System.AppDomain"/> to run KeePass multiple times while
    /// running tests. Use SetData and GetData methods to pass information
    /// between AppDomains.</remarks>
    /// </summary>
    public class KeePassAppDomain
    {
        private AppDomain appDomain;
        private static int instanceCount = 0;
        // {0} is instanceCount
        private const string friendlyName = "KeePass AppDomain #{0}";
        private const string keepassProcessName = "KeePass";
        private const string keepassExeName = "KeePass.exe";
        private const string configFileName = "KeePass.config.xml";
        private const string dbFileName = "test{0}.kdbx";
        private const string password = "test";
        
        private const int keepassStartTimeout = 3000; //msec

        /// <summary>
        /// Create a new AppDomain for running a new instance of KeePass
        /// </summary>
        public KeePassAppDomain()
        {
            instanceCount ++;
            
            this.appDomain = AppDomain.CreateDomain(
                string.Format(friendlyName, instanceCount),
                AppDomain.CurrentDomain.Evidence,
                AppDomain.CurrentDomain.SetupInformation);
        }

        /// <summary>
        /// Checks to see if the instance of KeePass connected with this
        /// AppDomain is running
        /// </summary>
        /// <returns>true if KeePass is running in this AppDomain</returns>
        public bool IsKeePassRunning()
        {
            string isRunningName = "KEPASS_IS_RUNNING";
            DoCallBack(delegate()
            {
                AppDomain.CurrentDomain.SetData(
                    isRunningName,
                    KeePass.Program.MainForm != null &&
                    !KeePass.Program.MainForm.IsDisposed);
            });
            return (bool)GetData(isRunningName);
        }

        /// <summary>
        /// Checks to see if any instance of KeePass is running (not just
        /// in this AppDomain).
        /// </summary>
        /// <returns>true if any KeePass.exe is running</returns>
        public bool IsAnyKeePassRunning()
        {
            return (Process.GetProcessesByName(keepassProcessName).Length > 0);
        }

        /// <summary>
        /// Starts KeePass in the current AppDomain
        /// </summary>
        /// <param name="exitAllFirst">set to true to send exit-all command
        /// and then wait for all other instances of KeePass to stop before
        /// starting the new instance</param>
        /// <param name="debug">set to true to enable the '--debug' 
        /// command line option</param>
        /// <param name="numDbFiles">Number of database file to load</param>
        /// <param name="newConfig">Setting to true copies a new defalut 
        /// configuration file to the working directory before starting 
        /// a new instance of KeePass</param>
        /// <returns>true if KeePass started successfully</returns>
        public bool StartKeePass(bool exitAllFirst, bool debug, int numDbFiles,
            bool newConfig)
        {
            const string keepassAlreadyRunName = "KEEPASS_ALREADY_RUN";

            DoCallBack(delegate()
            {
                AppDomain.CurrentDomain.SetData(keepassAlreadyRunName,
                    (KeePass.Program.MainForm != null));
            });
            bool kepassAlreadyRun = (bool)GetData(keepassAlreadyRunName);
            if (kepassAlreadyRun) {
                KeePassControl.ShowErrorMessage("Can only run KeePass once per AppDomain");
                return false;
            }

            if (numDbFiles < 1) {
                throw new ArgumentOutOfRangeException("numDbFiles");
            }

            /* vars */
            string debugDir;
            string debugConfigFile;
            List<string> testDbFiles;
            string keepassExeFile;
            string[] args;
            Assembly assembly;
            Stopwatch stopwatch;
            DialogResult result;

            if (exitAllFirst) {

                KeePassControl.ExitAll(); // close any open instances of keepass

                /* wait for processes to end */
                stopwatch = new Stopwatch();
                stopwatch.Start();
                while ((stopwatch.ElapsedMilliseconds < keepassStartTimeout) &&
                    IsAnyKeePassRunning()) {
                    Thread.Sleep(250);
                }
                stopwatch.Stop();

                /* verify all running instances of KeePass have ended */
                while (IsAnyKeePassRunning()) {
                    result = KeePassControl.ShowErrorMessage("Running instances of KeyPass did not stop within the specified timeout." +
                        "\n\nClick OK when all running instances of KeyPass are closed.", true);
                    if (result == DialogResult.Cancel) {
                        return false;
                    }
                }
            }

            /* verify directories */

            assembly = Assembly.GetAssembly(typeof(KeePass.Program));
            debugDir = Path.GetDirectoryName(assembly.Location);
            // really shouldn't need to check this
            if (!Directory.Exists(debugDir)) {
                KeePassControl.ShowErrorMessage("Debug directory '" + debugDir + "' does not exist." +
                    "\nIt should be the location of " + keepassExeName);
                return false;
            }

            /* verify files */

            keepassExeFile = Path.Combine(debugDir, keepassExeName);
            if (!File.Exists(keepassExeFile)) {
                KeePassControl.ShowErrorMessage("KeePass executable file '" + keepassExeFile + "' does not exist." +
                    "\nPlease make sure it is set up in References and 'Copy Local' property is set to true" +
                    "\nor fix 'keepassExe' in 'KeePassControl.cs'");
                return false;
            }

            /* copy files to working directory */
            if (newConfig) {
                debugConfigFile = Path.Combine(debugDir, configFileName);
                try {
                    File.WriteAllText(debugConfigFile, Properties.Resources.KeePass_config_xml);
                } catch (Exception ex) {
                    KeePassControl.ShowErrorMessage("Error writing config file '" + debugConfigFile + "'." +
                        "\n\n" + ex.Message);
                    return false;
                }
            }

            testDbFiles = new List<string>();
            for (int i = 1; i <= numDbFiles; i++) {
                string testDbFileN = Path.Combine(debugDir, string.Format(dbFileName, i));
                try {
                    File.WriteAllBytes(testDbFileN, Properties.Resources.test_kdbx);
                    testDbFiles.Add(testDbFileN);
                } catch (Exception ex) {
                    KeePassControl.ShowErrorMessage("Error writing database file '" + testDbFileN + "'." +
                        "\n\n" + ex.Message);
                    return false;
                }
            }

            /* start keepass with test1.kdbx db */
            try {
                args = new string[] { 
                    testDbFiles[0],
                    "-pw:" + password,
                    "--debug"
                };

                Thread keepassThread = new Thread((ThreadStart)delegate()
                {
                    this.appDomain.ExecuteAssembly(Assembly.GetAssembly(typeof(KeePass.Program)).Location);
                });
                keepassThread.SetApartmentState(ApartmentState.STA);
                keepassThread.Start();
                DoCallBack(delegate()
                {
                    while (KeePass.Program.MainForm == null || !KeePass.Program.MainForm.Visible) {

                    }                    
                });
            } catch (Exception ex) {
                KeePassControl.ShowErrorMessage("An exception occured while starting KeePass" +
                    "\n\n" + ex.ToString());
                return false;
            }

            /* wait for KeyPass to open */
            stopwatch = new Stopwatch();
            stopwatch.Start();
            while ((stopwatch.ElapsedMilliseconds < keepassStartTimeout) &&
                ((KeePass.Program.MainForm == null) ||
                (KeePass.Program.MainForm.PluginHost == null))) {

                Thread.Sleep(250);
            }
            /* wait for file to open if we asked for at least one file */
            if (numDbFiles >= 1) {
                while ((stopwatch.ElapsedMilliseconds < keepassStartTimeout) &&
                    !KeePass.Program.MainForm.IsAtLeastOneFileOpen()) {

                    Thread.Sleep(250);
                }
            }
            stopwatch.Stop();

            /* verify that program started */
            while (KeePass.Program.MainForm == null) {
                result = KeePassControl.ShowErrorMessage("KeePass did not start within the specified timeout." +
                    "\n\nClick OK when KeyPass has started.", true);
                if (result == DialogResult.Cancel) {
                    return false;
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
                        InvokeMainForm(delegate()
                        {
                            KeePass.Program.MainForm.OpenDatabase(ioConnection, compositeKey, true);
                        });
                    } catch (Exception ex) {
                        KeePassControl.ShowErrorMessage("An exception occured while opening additional database file" +
                            "\n\n" + ex.Message);
                        return false;
                    }
                }
            }

            while (KeePass.Program.MainForm.PluginHost == null) {
                result = KeePassControl.ShowErrorMessage("Cannot get PluginHost object. Make sure a file is open in KeePass." +
                    "\n\nClick OK when file is open.", true);
                if (result == DialogResult.Cancel) {
                    return false;
                }
            }

            // plugins are disabled in config file so that none are loaded automatically
            // re-enable now so that we can get to the plugin dialog
            KeePass.App.AppPolicy.Current.Plugins = true;

            return true;
        }
        
        /// <summary>
        /// <see cref="System.AppDomain.getData"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object GetData(string name) {
            return this.appDomain.GetData(name);
        }

        /// <summary>
        /// <see cref="System.AppDomain.SetData"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public void SetData(string name, object data)
        {
            this.appDomain.SetData(name, data);
        }

        /// <summary>
        /// Executes callBackDelegate in this AppDomain
        /// </summary>
        /// <param name="callBackDelegate">method to execute</param>
        public void DoCallBack(CrossAppDomainDelegate callBackDelegate)
        {
            this.appDomain.DoCallBack(callBackDelegate);
        }

        /// <summary>
        /// Invokes method on the MainForm thread of KeePass
        /// </summary>
        /// <remarks>This is required for any operations that interact with
        /// the KeePass UI (which is most everything)</remarks>
        /// <param name="methodInvoker">the method to invoke</param>
        public void InvokeMainForm(CrossAppDomainDelegate callBackDelegate)
        {
            DoCallBack(delegate()
            {
                Form mainWindow = KeePass.Program.MainForm;
                MethodInvoker methodInvoker = new MethodInvoker(callBackDelegate);
                if (mainWindow != null && !mainWindow.IsDisposed) {
                    if (mainWindow.InvokeRequired) {
                //        mainWindow.Invoke(methodInvoker);
                    } else {
                //        methodInvoker.Invoke();
                    }
                }
            });
        }
    }
}
