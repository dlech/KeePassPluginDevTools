using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Linq;
using System.IO;

namespace PlgxBuilder
{
  public class Program
  {    
    const string xsdFile = "Microsoft.Build.Plgx.xsd";

    const string argError =
      "Expected single argument containing build configuration xml";

    public static int Main(string[] args)
    {


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

        var document = new XmlDocument();
        document.Load(xsdFile);
        XmlReader rdr = XmlReader.Create(new StringReader(document.InnerXml), readerSettings);
        
        while (rdr.Read()) { }

        if (!isValid) {
          return 1;
        }

      } catch (XmlException xmlEx) {
        Console.WriteLine (argError);
        Console.WriteLine (xmlEx.Message);
        return 1;
      }

      return 0;
    }
  }
}

