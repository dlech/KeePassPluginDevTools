// based largely on / copied from PlgxPlugin.cs from KeePass

using System;
using System.IO;
using KeePass.Plugins;
using KeePassLib.Resources;
using System.Collections.Generic;
using KeePassLib;
using KeePassLib.Utility;
using System.Diagnostics;

namespace PlgxInspector
{
  public class Program
  {
    public const string PlgxExtension = "plgx";
    
    private const uint PlgxSignature1 = 0x65D90719;
    private const uint PlgxSignature2 = 0x3DDD0503;
    private const uint PlgxVersion = 0x00010000;
    private const uint PlgxVersionMask = 0xFFFF0000;
    
    private const ushort PlgxEOF = 0;
    private const ushort PlgxFileUuid = 1;
    private const ushort PlgxBaseFileName = 2;
    private const ushort PlgxBeginContent = 3;
    private const ushort PlgxFile = 4;
    private const ushort PlgxEndContent = 5;
    private const ushort PlgxCreationTime = 6;
    private const ushort PlgxGeneratorName = 7;
    private const ushort PlgxGeneratorVersion = 8;
    private const ushort PlgxPrereqKP = 9; // KeePass version
    private const ushort PlgxPrereqNet = 10; // .NET Framework version
    private const ushort PlgxPrereqOS = 11; // Operating system
    private const ushort PlgxPrereqPtr = 12; // Pointer size
    private const ushort PlgxBuildPre = 13;
    private const ushort PlgxBuildPost = 14;
    
    private const ushort PlgxfEOF = 0;
    private const ushort PlgxfPath = 1;
    private const ushort PlgxfData = 2;

    const string argError =
      "expected plgx file as only argument";

    public static int Main(string[] args)
    {
      if (args.Length != 1 ||
          !args[0].EndsWith (".plgx", StringComparison.OrdinalIgnoreCase) ||
          !File.Exists(args[0]) )
      {
        Console.WriteLine (argError);
        return 1;
      }

      try {
        var reader = new BinaryReader (File.OpenRead (args[0]));
        var plgx = ReadFile(reader);
        PrintData(plgx);
      } catch (Exception ex) {
        Console.WriteLine (ex.Message);
        return 1;
      }
      return 0;
    }

    public static PlgxInfo ReadFile(BinaryReader br)
    {
      var plgx = new PlgxInfo ();

      uint uSig1 = br.ReadUInt32();
      uint uSig2 = br.ReadUInt32();
      plgx.Version = br.ReadUInt32();
      
      if ((uSig1 != PlgxSignature1) || (uSig2 != PlgxSignature2))
        throw new Exception ("Invalid signature");
      if((plgx.Version & PlgxVersionMask) > (PlgxVersion & PlgxVersionMask))
        throw new PlgxException(KLRes.FileVersionUnsupported);
      
      string strPluginPath = null;
      string strTmpRoot = null;
      bool? bContent = null;
      string strBuildPre = null, strBuildPost = null;
      
      while(true)
      {
        KeyValuePair<ushort, byte[]> kvp = ReadObject(br);
        
        if(kvp.Key == PlgxEOF) break;
        else if(kvp.Key == PlgxFileUuid)
          plgx.FileUuid = new PwUuid(kvp.Value);
        else if(kvp.Key == PlgxBaseFileName)
          plgx.BaseFileName = StrUtil.Utf8.GetString(kvp.Value);
        else if(kvp.Key == PlgxCreationTime)
          plgx.CreationTime = StrUtil.Utf8.GetString(kvp.Value);
        else if(kvp.Key == PlgxGeneratorName)
          plgx.GeneratorName = StrUtil.Utf8.GetString(kvp.Value);
        else if(kvp.Key == PlgxGeneratorVersion)
          plgx.GeneratorVersion = MemUtil.BytesToUInt64(kvp.Value);
        else if(kvp.Key == PlgxPrereqKP)
          plgx.PrereqKP = MemUtil.BytesToUInt64(kvp.Value);
        else if(kvp.Key == PlgxPrereqNet)
          plgx.PrereqNet = MemUtil.BytesToUInt64(kvp.Value);
        else if(kvp.Key == PlgxPrereqOS)
          plgx.PrereqOS = StrUtil.Utf8.GetString(kvp.Value);
        else if(kvp.Key == PlgxPrereqPtr)
          plgx.PrereqPtr = MemUtil.BytesToUInt32(kvp.Value);
        else if(kvp.Key == PlgxBuildPre)
          plgx.BuildPre = StrUtil.Utf8.GetString(kvp.Value);
        else if(kvp.Key == PlgxBuildPost)
          plgx.BuildPost = StrUtil.Utf8.GetString(kvp.Value);
        else if(kvp.Key == PlgxBeginContent)
        {
          if(bContent.HasValue)
            throw new PlgxException(KLRes.FileCorrupted);

          bContent = true;
        }
        else if(kvp.Key == PlgxFile)
        {
          if(!bContent.HasValue || !bContent.Value)
            throw new PlgxException(KLRes.FileCorrupted);

          var file = ExtractFile(kvp.Value);
          if (file != null) {
            plgx.Files.Add(file.Value);
          }
        }
        else if(kvp.Key == PlgxEndContent)
        {
          if(!bContent.HasValue || !bContent.Value)
            throw new PlgxException(KLRes.FileCorrupted);
          
          bContent = false;
        }
        else { 
          // TODO - do we want to list extra data?
        }
      }
           
      return plgx;
    }

    private static KeyValuePair<ushort, byte[]> ReadObject(BinaryReader br)
    {
      try
      {
        ushort uType = br.ReadUInt16();
        uint uLength = br.ReadUInt32();
        byte[] pbData = ((uLength > 0) ? br.ReadBytes((int)uLength) : null);
        
        return new KeyValuePair<ushort, byte[]>(uType, pbData);
      }
      catch(Exception) { throw new PlgxException(KLRes.FileCorrupted); }
    }

    private static KeyValuePair<string, byte[]>? ExtractFile(byte[] pbData)
    {
      MemoryStream ms = new MemoryStream(pbData, false);
      BinaryReader br = new BinaryReader(ms);

      string strPath = null;
      byte[] pbContent = null;
      
      while(true)
      {
        KeyValuePair<ushort, byte[]> kvp = ReadObject(br);
        
        if(kvp.Key == PlgxfEOF) break;
        else if(kvp.Key == PlgxfPath)
          strPath = StrUtil.Utf8.GetString(kvp.Value);
        else if(kvp.Key == PlgxfData)
          pbContent = kvp.Value;
        else { Debug.Assert(false); }
      }
      
      br.Close();
      ms.Close();
      
      if (!string.IsNullOrEmpty (strPath) && pbContent != null)
        return new KeyValuePair<string, byte[]>(strPath, pbContent);

      Debug.Assert (false);
      return null;
    }

    public static void ExtractFile(string strTmpRoot, byte[] pbContent) {

      string strPath = null;

      string strTmpFile =
        UrlUtil.EnsureTerminatingSeparator (strTmpRoot, false) +
          UrlUtil.ConvertSeparators (strPath);
    
      string strTmpDir = UrlUtil.GetFileDirectory (strTmpFile, false, true);
      if (!Directory.Exists (strTmpDir))
        Directory.CreateDirectory (strTmpDir);
    
      byte[] pbDecompressed = MemUtil.Decompress (pbContent);
      File.WriteAllBytes (strTmpFile, pbDecompressed);     
    }

    private static void PrintData(PlgxInfo plgx)
    {
      Console.WriteLine (".plgx File Version:           {0}", plgx.Version >> 16);
      Console.WriteLine ("UUID:                         {0}", plgx.FileUuid.ToHexString ());
      Console.WriteLine ("Base File Name:               {0}", plgx.BaseFileName);
      Console.WriteLine ("Creation Time:                {0}", plgx.CreationTime);
      Console.WriteLine ("Generator Name:               {0}", plgx.GeneratorName);
      Console.WriteLine ("Generator Version:            {0}",
                         StrUtil.VersionToString (plgx.GeneratorVersion));
      Console.WriteLine ("Prerequsite KeePass Version:  {0}",
                         StrUtil.VersionToString (plgx.PrereqKP));
      Console.WriteLine ("Prerequsite .NET Version:     {0}",
                         StrUtil.VersionToString (plgx.PrereqNet));
      Console.WriteLine ("Prerequsite Operating System: {0}", plgx.PrereqOS);
      Console.WriteLine ("Prerequsite Pointer size:     {0}", plgx.PrereqPtr);
      Console.WriteLine ("Pre-build Command:");
      Console.WriteLine (plgx.BuildPre);
      Console.WriteLine ("Post-build Command:");
      Console.WriteLine (plgx.BuildPost);
      Console.WriteLine ("Files:");
      foreach (var file in plgx.Files) {
        // TODO - make bytes more readable with k or M
        Console.WriteLine ("{0} ({1} bytes)", file.Key, file.Value.Length);
      }
    }
  }
}

