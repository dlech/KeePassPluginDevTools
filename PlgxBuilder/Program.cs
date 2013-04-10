using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Linq;
using System.IO;
using System.Xml.Serialization;

namespace KeePassPluginDevTools.PlgxBuilder
{
  public class Program
  {    
    const string xsdFile = "Microsoft.Build.Plgx.xsd";

    const string argError =
      "Expected single argument containing build configuration xml";

    public static int Main(string[] args)
    {     
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

      return 0;
    }
  }
}

