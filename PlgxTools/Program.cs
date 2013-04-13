using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using NDesk.Options;
using KeePassLib.Utility;
using KeePassLib;
using System.Reflection;

[assembly:AssemblyVersion("0.1.0.*")]
[assembly:AssemblyFileVersion("0.1.0.0")]

namespace KeePassPluginDevTools.PlgxTools
{ 
  public class Program
  {    
    [Flags()]
    private enum Command
    {      
      Help = 0, // default
      Build,
      List,
      Extract,
      Package
    }

    const string xsdFile = "Microsoft.Build.Plgx.xsd";
    const string argError =
      "Expected single argument containing build configuration xml";

    public static int Main (string[] args)
    {
#if DEBUG
      // set args here for debugging
      args = new[] { "--build", "a", "-c=test.xml" };
#endif
      var selectedCommand = new Command ();
      string input = null;
      string output = null;
      string config = null;

      var options = new OptionSet ()
      {
        { "b|build", "create plgx file",
          v => { if (v != null) selectedCommand |= Command.Build; } },
        { "l|list", "list contents of plgx file",
          v => { if (v != null) selectedCommand |= Command.List; } },
        { "e|extract", "extract file(s) from plgx",
          v => { if (v != null) selectedCommand |= Command.Extract; } },
        { "p|package", "package plgx for distribution",
          v => { if (v != null) selectedCommand |= Command.Package; } },
        { "h|help|?", "show usage",
          v => { if (v != null) selectedCommand |= Command.Help; } },
        { "i|in|input=", "input file or directory",
          v => { input = v; } },
        { "o|out|output=", "output file or directory",
          v => { output = v; } },
        { "c|conf|config|configuration=", "configuration file",
          v => { config = v; } }
      };

      try {
        var extras = options.Parse (args);

        if (input == null && extras.Count >= 1) {
          input = extras [0];
          extras.RemoveAt (0);
        }
        if (output == null && extras.Count >= 1) {
          output = extras [0];
          extras.RemoveAt (0);
        }
        if (config == null && extras.Count >= 1) {
          config = extras [0];
          extras.RemoveAt (0);
        }

        if (selectedCommand == Command.Build && input != null && output == null) {
          output = Path.Combine (input, "..");
        }
#if DEBUG
        Console.WriteLine ("input:  " + input);
        Console.WriteLine ("output: " + output);
        Console.WriteLine ("config: " + config);
        Console.WriteLine ("extras: " + string.Join (", ", extras));
        Console.WriteLine ();
#endif
      } catch {
        selectedCommand = Command.Help;
      }

      if (selectedCommand == Command.Help ||
      // build requires source dir
        (selectedCommand == Command.Build && (input == null || output == null)) ||
      // build requires source dir
        (selectedCommand == Command.List && input == null) ||
      // selected commands are mutually exclusive
        Math.Log ((double)selectedCommand, 2) % 1 != 0) {
        Console.WriteLine (GetUsage ());
        return 1;
      }

      switch (selectedCommand) {
        #region Build Command
      case Command.Build:

        args = new string[]{ @"<?xml version=""1.0"" encoding=""utf-8""?>
<PlgxConfiguration>
  <Prerequisites>
    <KeePassVersion>2.22</KeePassVersion>
    <DotNetVersion>4.0</DotNetVersion>
    <OS>Unix</OS>
    <PointerSize>4</PointerSize>
  </Prerequisites>
</PlgxConfiguration>" };

        if (args.Length != 1) {
          Console.WriteLine (argError);
          return 1;
        }

        try {
          var plgx = new PlgxInfo ();
          plgx.Version = PlgxInfo.PlgxVersion1;
          plgx.FileUuid = new PwUuid(true);
          plgx.CreationTime = TimeUtil.SerializeUtc(DateTime.Now);
          var assm = Assembly.GetAssembly (typeof(Program)).GetName ();
          plgx.GeneratorName = assm.Name;
          plgx.GeneratorVersion = 
            StrUtil.ParseVersion (assm.Version.ToString ());

          if (config != null) {
            using (var configFileStream = File.OpenRead (config)) {
              var serializer = new XmlSerializer (typeof(PlgxConfiguration));
              var plgxConfig =
                serializer.Deserialize (configFileStream) as PlgxConfiguration;
              if (plgxConfig.Prerequisites != null) {
                if (!string.IsNullOrWhiteSpace(plgxConfig.Prerequisites.KeePassVersion)) {
                  plgx.PrereqKP = StrUtil.ParseVersion (plgxConfig.Prerequisites.KeePassVersion);
                }
                if (!string.IsNullOrWhiteSpace(plgxConfig.Prerequisites.DotNetVersion)) {
                  plgx.PrereqNet = StrUtil.ParseVersion (plgxConfig.Prerequisites.DotNetVersion);
                }
                if (plgxConfig.Prerequisites.OSSpecified) {
                  plgx.PrereqOS = plgxConfig.Prerequisites.OS.GetValue ();
                }
                if (plgxConfig.Prerequisites.PointerSizeSpecified) {
                  plgx.PrereqPtr = uint.Parse (plgxConfig.Prerequisites.PointerSize.GetValue ());
                }
              }
              if (plgxConfig.BuildCommands != null) {
                if (!string.IsNullOrWhiteSpace (plgxConfig.BuildCommands.PreBuild)) {
                  plgx.BuildPre = plgxConfig.BuildCommands.PreBuild;
                }
                if (!string.IsNullOrWhiteSpace (plgxConfig.BuildCommands.PostBuild)) {
                  plgx.BuildPost = plgxConfig.BuildCommands.PostBuild;
                }
              }
              #if DEBUG
              Console.WriteLine ("--Generator--");
              Console.WriteLine ("Version: 0x{0:X8}", plgx.Version);
              Console.WriteLine ("FileUuid: {0}", plgx.FileUuid.ToHexString ());
              Console.WriteLine ("BaseFileName: {0}", plgx.BaseFileName);
              Console.WriteLine ("CreationTime: {0}", plgx.CreationTime);
              Console.WriteLine ("GeneratorName: {0}", plgx.GeneratorName);
              Console.WriteLine ("GeneratorVersion: 0x{0:X16} ({1})",
                                 plgx.GeneratorVersion, 
                                 StrUtil.VersionToString (plgx.GeneratorVersion));
              Console.WriteLine ("--Prerequisites--");
              Console.WriteLine ("KeePass Version: {0}",
                                 plgxConfig.Prerequisites == null || 
                                 plgxConfig.Prerequisites.KeePassVersion == null ?
                                 PlgxInfo.none : 
                                 string.Format ("{0} (0x{1:X16})",
                             plgxConfig.Prerequisites.KeePassVersion,
                             plgx.PrereqKP));
              Console.WriteLine (".NET Version: {0}",
                                 plgxConfig.Prerequisites == null || 
                                 plgxConfig.Prerequisites.DotNetVersion == null ?
                                 PlgxInfo.none :
                                 string.Format ("{0} (0x{1:X16})",
                             plgxConfig.Prerequisites.DotNetVersion,
                             plgx.PrereqNet));
              Console.WriteLine ("OS Version: {0}",
                                 plgxConfig.Prerequisites == null || 
                                 !plgxConfig.Prerequisites.OSSpecified ?
                                 PlgxInfo.none :
                                 string.Format ("{0} ({1})",
                                 plgxConfig.Prerequisites.OS.GetValue (),
                                 plgx.PrereqOS));
              Console.WriteLine ("Pointer Size: {0}",
                                 plgxConfig.Prerequisites == null || 
                                 !plgxConfig.Prerequisites.PointerSizeSpecified ?
                                 PlgxInfo.none :
                                 string.Format ("{0} ({1})",
                                 plgxConfig.Prerequisites.PointerSize.GetValue (),
                                 plgx.PrereqPtr));              
              Console.WriteLine ("--Build Commands--");
              Console.WriteLine ("Pre-build:");
              Console.WriteLine (plgxConfig.BuildCommands == null || 
                                 plgxConfig.BuildCommands.PreBuild == null ?
                                 PlgxInfo.none : plgx.BuildPre);              
              Console.WriteLine ("Post-build:");
              Console.WriteLine (plgxConfig.BuildCommands == null || 
                                 plgxConfig.BuildCommands.PostBuild == null ?
                                 PlgxInfo.none : plgx.BuildPost);
              #endif
            }
          }
        } catch (Exception ex) {
          Console.WriteLine (ex.Message);
          return 1;
        }
        break;
        #endregion

        #region Extract Command
      case Command.Extract:
        Console.WriteLine ("Not implemented.");
        return 1;
        #endregion

        #region List Command
      case Command.List:
        try {
          var plgx = PlgxInfo.ReadFile (File.OpenRead (input));
          Console.WriteLine (plgx.ToString (true));
        } catch (Exception ex) {
          Console.WriteLine (ex.Message);
          return 1;
        }   
        break;
        #endregion

        #region Package Command
      case Command.Package:
        Console.WriteLine ("Not implemented.");
        return 1;
        #endregion
      }
      return 0;
    }

    private static string GetUsage ()
    {
      string executable = 
        Environment.OSVersion.Platform == PlatformID.Win32Windows ?
          "PlgxTool.exe" : "plgx-tool";
      var line = "{0,-4}{1,-12}{2}\n";

      var builder = new StringBuilder ();
      builder.AppendLine ();
      builder.AppendLine ("Usage:");

      /* Build syntax */
      builder.Append (executable);
      builder.Append (" --build [--in=]<source-directory> ");
      builder.Append ("[[--out=]<destination-directory>] ");
      builder.Append ("[[--config=]<configuration-file>] ");
      builder.AppendLine ();

      /* List syntax */
      builder.Append (executable);
      builder.Append (" --list [--in=]<source-file> ");
      builder.AppendLine ();
      builder.AppendLine ();

      //    |                                --- ruler ---                                   |
      //    |00000000011111111112222222222333333333344444444445555555555666666666677777777778|                  
      //    |12345678901234567890123456789012345678901234567890123456789012345678901234567890|
      //    |x   x          x                                                                |

      /* Commands */
      builder.AppendLine ("Commands:");
      builder.AppendFormat (line, "-b", "--build",
                            "Builds .plgx from the specified source directory.");
      builder.AppendFormat (line, string.Empty, string.Empty,
                            "If the ouput directory is not specified, the .plgx file will be");
      builder.AppendFormat (line, string.Empty, string.Empty,
                            "created in the parent directory of the source directory.");
      builder.AppendFormat (line, "-l", "--list",
                            "Lists contents of .plgx file.");
      builder.AppendLine ();

      /* Options */
      builder.AppendLine ("Options:");
      builder.AppendFormat (line, "-i", "--in[put]",
                            "Input file or directory.");
      builder.AppendFormat (line, "-o", "--out[put]",
                            "Output file or directory.");
      builder.AppendFormat (line, "-c", "--config[uration]",
                            "Configuration file.");
      builder.AppendLine ();

      return builder.ToString ();
    }
  }
}
