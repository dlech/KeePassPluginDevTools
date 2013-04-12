using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Linq;
using System.IO;
using System.Xml.Serialization;
using NDesk.Options;
using KeePassPluginDevTools.PlgxBuilder;

namespace KeePassPluginDevTools.PlgxTools
{
  public class Program
  {    
    [Flags()]
    private enum Command {      
      Help = 0, // default
      Build,
      List,
      Extract,
      Package
    }

    const string xsdFile = "Microsoft.Build.Plgx.xsd";

    const string argError =
      "Expected single argument containing build configuration xml";

    public static int Main(string[] args)
    {
#if DEBUG
      // set args here for debugging
      args = new[] { "--list", "/tmp/KeeAgent.plgx" };
#endif
      var selectedCommand = new Command();
      string sourceDir = null;
      string target = null;

      var options = new OptionSet()
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
        { "sd|source-dir|source-directory=", "source project directory",
          v => { sourceDir = v; } },
        { "dd|dest-dir|destination-directory=", "output destination directory",
          v => { target = v; } },
        { "f|file|sf|source-file=", "target file",
          v => { target = v; } }
      };

      try {
        var extras = options.Parse (args);

        if (target == null && extras.Count >= 1) {
          target = extras[0];
          extras.RemoveAt(0);
        }

        Console.WriteLine (string.Join (", ", extras));

      } catch {
        selectedCommand = Command.Help;
      }

      if (selectedCommand == Command.Help ||
          // build requires source dir
          (selectedCommand == Command.Build && (sourceDir == null || target == null)) ||
          // build requires source dir
          (selectedCommand == Command.List && target == null) ||
          // selected commands are mutually exclusive
          Math.Log ((double)selectedCommand, 2) % 1 != 0) 
      {
        // TODO - add PrintUsage function
        Console.WriteLine ("Usage...");
        return 1;
      }

      switch(selectedCommand)
      {
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
        var isValid = true;
        var readerSettings = new XmlReaderSettings();
        readerSettings.Schemas.Add(null, xsdFile);
        readerSettings.ValidationType = ValidationType.Schema;
        readerSettings.ValidationEventHandler +=
          (object sender, ValidationEventArgs e) => 
        {
          Console.WriteLine ("{0}: {1}",
                             e.Severity == XmlSeverityType.Error ?
                             "Error" : "Warning",
                             e.Message);
          Console.WriteLine ("At line {0}:{1}",
                             e.Exception.LineNumber,
                             e.Exception.LinePosition);
          if (e.Severity == XmlSeverityType.Error) {
            isValid = false;
          }
        };

        XmlReader reader = XmlReader.Create(new StringReader(args[0]), readerSettings);
        var serializer = new XmlSerializer(typeof(PlgxConfiguration));
        var config = serializer.Deserialize (reader) as PlgxConfiguration;

        if (!isValid) {
          return 1;
        }

        Console.WriteLine ("--Prerequisites--");
        Console.WriteLine ("KeePass Version: {0}", config.Prerequisites.KeePassVersion);
        Console.WriteLine (".NET Version: {0}", config.Prerequisites.DotNetVersion);
        Console.WriteLine ("OS Version: {0}", config.Prerequisites.OS);
        Console.WriteLine ("Pointer Size: {0}", config.Prerequisites.PointerSize);

      } catch (Exception ex) {
        Console.WriteLine (ex.Message);
        return 1;
      }
        break;
      case Command.Extract:
        Console.WriteLine("Not implemented.");
        return 1;
      case Command.List:
        try {
          var plgx = PlgxInfo.ReadFile(File.OpenRead (target));
          Console.WriteLine(plgx.ToString (true));
        } catch (Exception ex) {
          Console.WriteLine (ex.Message);
          return 1;
        }   
        break;
      case Command.Package:
        Console.WriteLine("Not implemented.");
        return 1;
      }
      return 0;
   }
  }
}
