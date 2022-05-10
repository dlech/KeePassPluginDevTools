using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using NDesk.Options;
using KeePassLib.Utility;
using KeePassLib;
using System.Reflection;
using System.Collections.Generic;

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
// #if DEBUG
//       // set args here for debugging
//       args = new[] { "--build", @"..\..\..\SamplePlugin\",
//         "../../../SamplePlugin/bin/Debug/",
//         "-c=test.xml" };
// #endif
      var selectedCommand = new Command ();
      string input = null;
      string output = null;
      string config = null;
      bool verbose = false;

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
          v => { config = v; } },
        { "v|verbose", "configuration file",
          v => { verbose = v != null; } }
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

        input = GetFullPath(input);
        output = GetFullPath(output);
        config = GetFullPath(config);

        if (verbose) {
          Console.WriteLine ("input:  " + input);
          Console.WriteLine ("output: " + output);
          Console.WriteLine ("config: " + config);
          Console.WriteLine ("extras: " + string.Join (", ", extras));
          Console.WriteLine ();
        }
      } catch (Exception ex) {
        Console.WriteLine (ex.Message);
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
          try {
            // populate common information for all plgx
            var plgx = new PlgxInfo ();
            plgx.Version = PlgxInfo.PlgxVersion1;
            plgx.FileUuid = new PwUuid (true);
            plgx.CreationTime = TimeUtil.SerializeUtc (DateTime.Now);
            var assm = Assembly.GetAssembly (typeof(Program)).GetName ();
            plgx.GeneratorName = assm.Name;
            plgx.GeneratorVersion = 
            StrUtil.ParseVersion (assm.Version.ToString ());

            // read the optional config file to get the rest of the plgx header
            // information
            if (config != null) {
              var configDoc = new XmlDocument();
              configDoc.Load (config);
              // mono xbuild adds namespaces for some reason, so we have to
              // strip them or else the serializer fails
              configDoc = XmlNamespaceStripper.StripNamespace (configDoc);

              var serializer = new XmlSerializer (typeof(PlgxConfiguration));
              if (verbose) {
#if DEBUG
                var writer = new XmlTextWriter(Console.OpenStandardOutput(), Encoding.UTF8);
                writer.Formatting = Formatting.Indented;
                configDoc.Save (writer);
                Console.WriteLine ();
#endif
                serializer.UnknownAttribute += (sender, e) =>
                  Console.WriteLine ("Unknown attribute: {0} at {1}:{2}",
                                     e.Attr.Name, e.LineNumber, e.LinePosition);
                serializer.UnknownElement += (sender, e) => 
                  Console.WriteLine ("Unknown element: {0} at {1}:{2}",
                                     e.Element.Name, e.LineNumber, e.LinePosition);
                serializer.UnknownNode += (sender, e) => 
                  Console.WriteLine ("Unknown node: {0} at {1}:{2}",
                                     e.Name, e.LineNumber, e.LinePosition);
                serializer.UnreferencedObject += (sender, e) => 
                  Console.WriteLine ("Unreferenced object: {0}",
                                     e.UnreferencedId);                
              }
              configDoc.Save (config);
              var plgxConfig =
                serializer.Deserialize (File.OpenRead (config)) as PlgxConfiguration;
              if (plgxConfig.Prerequisites != null) {
                if (!string.IsNullOrWhiteSpace (plgxConfig.Prerequisites.KeePassVersion)) {
                  plgx.PrereqKP = StrUtil.ParseVersion (plgxConfig.Prerequisites.KeePassVersion);
                }
                if (!string.IsNullOrWhiteSpace (plgxConfig.Prerequisites.DotNetVersion)) {
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
            }  

            // read the .csproj file to find which files we need to include in
            // the plgx
            var projectFiles = Directory.GetFiles (input, "*.csproj");
            if (projectFiles.Length != 1) {
              Console.WriteLine ("Source directory must contain one and only on .csproj file");
              return 1;
            }
            var project = new XmlDocument ();
            project.Load (projectFiles [0]);

            var assemblyNames = project.GetElementsByTagName ("AssemblyName");
            foreach (XmlNode assemblyName in assemblyNames) {
              plgx.BaseFileName = assemblyName.InnerText;
            }

            // include any files from <PlgxExtras> elements
            var plgxExtras = project.GetElementsByTagName ("PlgxExtras");
            foreach (XmlNode extra in plgxExtras) {
              foreach (XmlNode child in extra.ChildNodes) {
                if (child.LocalName == "Item") {
                  var source = child.Attributes ["Source"];
                  var destination = child.Attributes ["Destination"];
                  if (source != null && !string.IsNullOrWhiteSpace (source.Value) &&
                    destination != null && !string.IsNullOrWhiteSpace (destination.Value)) {
                    var sourcePath = UrlUtil.ConvertSeparators (source.Value);
                    sourcePath = Path.Combine (input, sourcePath);
                    sourcePath = Path.GetFullPath (sourcePath);
                    plgx.AddFileFromDisk (sourcePath, destination.Value);
                  }
                }
              }
            }

            // include all of the project files unless specifically excluded
            var itemGroups = project.GetElementsByTagName ("ItemGroup");
            foreach (XmlNode itemGroup in itemGroups) {
              // make copy of nodes so that we can delete them inside of the for
              // loop if we need to
              var children = new List<XmlNode> ();
              foreach (XmlNode child in itemGroup.ChildNodes) {
                children.Add (child);
              }
              foreach (XmlNode child in children) {
                if (child.LocalName == "Reference") {
                  if (child.ChildNodes.Cast<XmlNode>().Any(n => n.LocalName == "ExcludeFromPlgx")) {
                    itemGroup.RemoveChild(child);
                    continue;
                  }

                  foreach (XmlNode childMetadata in child.ChildNodes) {
                    var assemblyPath = Path.GetFullPath (
                    Path.Combine (input, UrlUtil.ConvertSeparators (childMetadata.InnerText)));
                    if (childMetadata.Name == "HintPath" &&
                        (child.Attributes ["Include"] == null ||
                         (!child.Attributes ["Include"].Value.StartsWith("KeePass,") &&
                          !child.Attributes ["Include"].Value.StartsWith("KeePass.exe"))))
                    {
                      if (!assemblyPath.StartsWith (input)) {
                        // TODO - do we really want a fixed folder name here?
                        childMetadata.InnerText = @"References\" + Path.GetFileName (assemblyPath);
                      }
                      plgx.AddFileFromDisk (assemblyPath, childMetadata.InnerText);
                    }
                  }
                  continue;
                }
                // technically, the Include attribute is a semicolon separated
                // list and can include wildcards, but KeePass is only looking
                // for single files here, so we should not have to take this
                // into account
                var includeFile = child.Attributes ["Include"];
                if (includeFile != null &&
                  !string.IsNullOrWhiteSpace (includeFile.Value))
                {
                  // skip "Include" files that are marked for exclusion from 
                  // the .plgx
                  var exclude = false;
                  foreach(XmlNode grandchild in child.ChildNodes)
                  {
                    if (grandchild.LocalName == "ExcludeFromPlgx") {
                      exclude = true;
                      break;
                    }
                  }
                  if (exclude) {
                    continue;
                  }

                  // copy all project items that have an Include attribute
                  var includeFilePath = Path.GetFullPath (
                  Path.Combine (input, UrlUtil.ConvertSeparators (includeFile.Value)));

                  // <ProjectReference> elements get deleted.
                  // If the <ProjectReference> element has <PlgxReference> child
                  // elements, new <Reference> elements will be created using the
                  // data from the <PlgxReference> element
                  if (child.LocalName == "ProjectReference") {
                    foreach (XmlNode projectItem in child.ChildNodes) {
                      if (projectItem.LocalName == "PlgxReference") {
                        var source = Path.GetFullPath (Path.Combine (input,
                        UrlUtil.ConvertSeparators (projectItem.InnerText)));
                        var destination = @"Reference\" + Path.GetFileName (source);
                        var projectReference =
                        project.CreateElement ("Reference", child.NamespaceURI);
                        projectReference.SetAttribute ("Include",
                                                    Path.GetFileName (destination));
                        var hintPath =
                        project.CreateElement ("HintPath", child.NamespaceURI);
                        hintPath.InnerText = destination;
                        projectReference.AppendChild (hintPath);
                        child.ParentNode.AppendChild (projectReference);
                        plgx.AddFileFromDisk (source, destination);
                      }
                    }

                    child.ParentNode.RemoveChild (child);
                    continue;
                  }
                  plgx.AddFileFromDisk (includeFilePath, includeFile.Value);
                }
              }
            }
            // write the in-memory project xml document (.csproj) to the plgx
            // instead of the file on disk since we may have changed it
            using (var stream = new MemoryStream()) { 
              var writer = new XmlTextWriter (stream, Encoding.UTF8);
              writer.Formatting = Formatting.Indented;
              project.Save (writer);
              plgx.Files.Add (Path.GetFileName (projectFiles [0]), stream.ToArray ());
#if DEBUG
              if (verbose) {
                Console.WriteLine (Encoding.UTF8.GetString (
                  plgx.Files[Path.GetFileName (projectFiles[0])]));
                Console.WriteLine ();
              }
#endif
            }
            if (verbose) {
              Console.WriteLine (plgx.ToString (true));
            }
            plgx.WriteFile (output);
          } catch (Exception ex) {
            Console.WriteLine (ex.Message);
            Console.WriteLine (ex.StackTrace);
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
      const string line = "{0,-4}{1,-12}{2}\n";

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
      builder.AppendFormat (line, "-v", "--verbose",
                            "Print additional info when creating plgx.");
      builder.AppendFormat (line, string.Empty, string.Empty,
                            "Useful for troubleshooting.");
      builder.AppendLine ();

      return builder.ToString ();
    }

    /// <summary>
    /// Strips trailing separator, converts path separators and converts
    /// to full path
    /// </summary>
    private static string GetFullPath(string path) {
      if (string.IsNullOrWhiteSpace(path)) {
        return string.Empty;
      }
      path = UrlUtil.ConvertSeparators(path);
      return Path.GetFullPath(path);
    }
  }
}
