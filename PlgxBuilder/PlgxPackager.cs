using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using KeePassPluginDevTools.Control;

namespace KeePassPluginDevTools.Packager
{
  public class PlgxPackager
 {
    public static void CreatePackage(PlgxBuildOptions options, string[] dllList, string[] docList)
   {
      // get make sure project path is full path name
      options.projectPath = Path.GetDirectoryName(options.projectPath);

      // set working directory to parent of project
      Directory.SetCurrentDirectory(Path.Combine(options.projectPath, ".."));

      // copy any dlls we might need to the root of the project path
      for (int i = 0, len = dllList.Length; i < len; i++)
     {
        string destination = Path.Combine(options.projectPath, Path.GetFileName(dllList[i]));
        File.Copy(dllList[i], destination);
        dllList[i] = destination;
      }

      // delete bin and obj directories
      Directory.Delete(Path.Combine(options.projectPath, @"\bin"));
      Directory.Delete(Path.Combine(options.projectPath, @"\obj"));

      KeePassControl.CreatePlgx(options);

      // delete the dlls that we copied
      for (int i = 0, len = dllList.Length; i < len; i++)
     {
        File.Delete(dllList[i]);
      }
    }

  }
}
