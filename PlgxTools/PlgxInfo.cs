// based largely on / copied from PlgxPlugin.cs from KeePass

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using KeePassLib;
using KeePassLib.Resources;
using KeePassLib.Utility;
using System.Text;
using System.Reflection;

namespace KeePassPluginDevTools.PlgxTools
{
  public class PlgxInfo
  {
    /// <summary>
    /// The plgx file extension.
    /// </summary>
    public const string PlgxExtension = "plgx";
    public const string none = "<none>";
    
    private const uint PlgxSignature1 = 0x65D90719;
    private const uint PlgxSignature2 = 0x3DDD0503;
    public const uint PlgxVersion1 = 0x00010000;
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

    /// <summary>
    /// Gets or Sets the plgx file format version
    /// </summary>
    public uint Version { get; set; }

    /// <summary>
    /// Gets or sets the file UUID.
    /// </summary>
    public PwUuid FileUuid { get; set; }

    /// <summary>
    /// Gets or sets the name of the base file.
    /// </summary>
    public string BaseFileName { get; set; }   

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    public string CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the name of the generator.
    /// </summary>
    public string GeneratorName { get; set; }

    /// <summary>
    /// Gets or sets the generator version.
    /// </summary>
    public ulong GeneratorVersion { get; set; } 

    /// <summary>
    /// Gets or sets the prerequsite KeePass version required
    /// </summary>
    public ulong? PrereqKP { get; set; }

    /// <summary>
    /// Gets or sets the prerequsite .NET version.
    /// </summary>
    public ulong? PrereqNet { get; set; }

    /// <summary>
    /// Gets or sets the prerequsite operating system
    /// </summary>
    public string PrereqOS { get; set; }

    /// <summary>
    /// Gets or sets the prerequsite pointer size
    /// </summary>
    public uint? PrereqPtr { get; set; }

    /// <summary>
    /// Gets or sets the pre-build command
    /// </summary>
    public string BuildPre { get; set; }

    /// <summary>
    /// Gets or sets the post-build command
    /// </summary>
    public string BuildPost { get; set; }

    /// <summary>
    /// The list of files contained in the plgx.
    /// </summary>
    public IDictionary<string, byte[]> Files { get; private set; }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="KeePassPluginDevTools.PlgxTools.PlgxInfo"/> class.
    /// </summary>
    public PlgxInfo ()
    {
      Files = new Dictionary<string, byte[]> ();
    }

    public void WriteFile(string destDir)
    {
      string plgxFileName =
        Path.Combine (destDir, BaseFileName + "." + PlgxExtension);

      //PlgxCsprojLoader.LoadDefault(destDir, plgx);
      
      FileStream fs = new FileStream(plgxFileName, FileMode.Create,
                                     FileAccess.Write, FileShare.None);
      BinaryWriter writer = new BinaryWriter(fs);
      
      writer.Write(PlgxSignature1);
      writer.Write(PlgxSignature2);
      writer.Write(Version);
      WriteObject(writer, PlgxFileUuid, FileUuid.UuidBytes);
      WriteObject(writer, PlgxBaseFileName, StrUtil.Utf8.GetBytes(
        BaseFileName));
      WriteObject(writer, PlgxCreationTime, StrUtil.Utf8.GetBytes(
        CreationTime));
      WriteObject(writer, PlgxGeneratorName, StrUtil.Utf8.GetBytes(
        GeneratorName));
      WriteObject(writer, PlgxGeneratorVersion, MemUtil.UInt64ToBytes(
        GeneratorVersion));
           
      if (PrereqKP.HasValue) {
        WriteObject (writer, PlgxPrereqKP, MemUtil.UInt64ToBytes (PrereqKP.Value));
      }

      if(PrereqNet.HasValue) {
        WriteObject(writer, PlgxPrereqNet, MemUtil.UInt64ToBytes(PrereqNet.Value));
      }

      if (!string.IsNullOrEmpty (PrereqOS)) {
        WriteObject (writer, PlgxPrereqOS, StrUtil.Utf8.GetBytes (PrereqOS));
      }

      if(PrereqPtr.HasValue)
      {
        WriteObject(writer, PlgxPrereqPtr, MemUtil.UInt32ToBytes(PrereqPtr.Value));
      }

      if (!string.IsNullOrEmpty (BuildPre)) {
        WriteObject (writer, PlgxBuildPre, StrUtil.Utf8.GetBytes (BuildPre));
      }

      if (!string.IsNullOrEmpty (BuildPost)) {
        WriteObject (writer, PlgxBuildPost, StrUtil.Utf8.GetBytes (BuildPost));
      }
      
      WriteObject(writer, PlgxBeginContent, null);
      
      foreach (var file in Files) {
        AddFile (writer, file);
      }
      
      WriteObject(writer, PlgxEndContent, null);
      WriteObject(writer, PlgxEOF, null);
      
      writer.Close();
      fs.Close();
    }

    private static void WriteObject(BinaryWriter writer,
                                    ushort objetType,
                                    byte[] objectData)
    {
      writer.Write(objetType);
      writer.Write((uint)((objectData != null) ? objectData.Length : 0));
      if ((objectData != null) && (objectData.Length > 0)) {
        writer.Write (objectData);
      }
    }


    private static void AddFile(BinaryWriter writer,
                                KeyValuePair<string, byte[]> file)
    {
      
      var stream = new MemoryStream();
      var streamWriter = new BinaryWriter(stream);

      WriteObject(streamWriter, PlgxfPath, StrUtil.Utf8.GetBytes(file.Key));

      if(file.Value.LongLength >= (long)(int.MaxValue / 2)) // Max 1 GB
        throw new OutOfMemoryException();
      
      byte[] compressedData = MemUtil.Compress(file.Value);
      WriteObject(streamWriter, PlgxfData, compressedData);
      
      WriteObject(streamWriter, PlgxfEOF, null);
      
      WriteObject(writer, PlgxFile, stream.ToArray());
      streamWriter.Close();
      stream.Close();

      if(!MemUtil.ArraysEqual(MemUtil.Decompress(compressedData), file.Value))
        throw new InvalidOperationException();
    }


    /// <summary>
    /// Reads contents of plgx as PlgxInfo object.
    /// </summary>
    /// <returns>PlgxInfo object containing information from the plgx.</returns>
    /// <param name="stream">Stream containing plgx contents.</param>
    public static PlgxInfo ReadFile(Stream stream)
    {
      var reader = new BinaryReader (stream);
      var plgx = new PlgxInfo ();
      
      var signature1 = reader.ReadUInt32();
      var signature2 = reader.ReadUInt32();
      plgx.Version = reader.ReadUInt32();
      
      if ((signature1 != PlgxSignature1) || (signature2 != PlgxSignature2))
        throw new PlgxException ("Invalid signature at start of file");
      if((plgx.Version & PlgxVersionMask) > (PlgxVersion1 & PlgxVersionMask))
        throw new PlgxException(KLRes.FileVersionUnsupported);
      
      bool? content = null;
      
      while(true)
      {
        var pair = ReadObject(reader);
        
        if(pair.Key == PlgxEOF) break;
        else if(pair.Key == PlgxFileUuid)
          plgx.FileUuid = new PwUuid(pair.Value);
        else if(pair.Key == PlgxBaseFileName)
          plgx.BaseFileName = StrUtil.Utf8.GetString(pair.Value);
        else if(pair.Key == PlgxCreationTime)
          plgx.CreationTime = StrUtil.Utf8.GetString(pair.Value);
        else if(pair.Key == PlgxGeneratorName)
          plgx.GeneratorName = StrUtil.Utf8.GetString(pair.Value);
        else if(pair.Key == PlgxGeneratorVersion)
          plgx.GeneratorVersion = MemUtil.BytesToUInt64(pair.Value);
        else if(pair.Key == PlgxPrereqKP)
          plgx.PrereqKP = MemUtil.BytesToUInt64(pair.Value);
        else if(pair.Key == PlgxPrereqNet)
          plgx.PrereqNet = MemUtil.BytesToUInt64(pair.Value);
        else if(pair.Key == PlgxPrereqOS)
          plgx.PrereqOS = StrUtil.Utf8.GetString(pair.Value);
        else if(pair.Key == PlgxPrereqPtr)
          plgx.PrereqPtr = MemUtil.BytesToUInt32(pair.Value);
        else if(pair.Key == PlgxBuildPre)
          plgx.BuildPre = StrUtil.Utf8.GetString(pair.Value);
        else if(pair.Key == PlgxBuildPost)
          plgx.BuildPost = StrUtil.Utf8.GetString(pair.Value);
        else if(pair.Key == PlgxBeginContent)
        {
          if(content.HasValue)
            throw new PlgxException(KLRes.FileCorrupted);
          
          content = true;
        }
        else if(pair.Key == PlgxFile)
        {
          if(!content.HasValue || !content.Value)
            throw new PlgxException(KLRes.FileCorrupted);
          
          var file = ExtractFile(pair.Value);
          if (file != null) {
            plgx.Files.Add(file.Value);
          }
        }
        else if(pair.Key == PlgxEndContent)
        {
          if(!content.HasValue || !content.Value)
            throw new PlgxException(KLRes.FileCorrupted);
          
          content = false;
        }
        else { 
          // TODO - do we want to list extra data?
        }
      }
      
      return plgx;
    }
    
    private static KeyValuePair<ushort, byte[]> ReadObject(BinaryReader reader)
    {
      try
      {
        ushort dataType = reader.ReadUInt16();
        uint length = reader.ReadUInt32();
        byte[] dataValue = ((length > 0) ? reader.ReadBytes((int)length) : null);
        
        return new KeyValuePair<ushort, byte[]>(dataType, dataValue);
      }
      catch(Exception) { throw new PlgxException(KLRes.FileCorrupted); }
    }
    
    private static KeyValuePair<string, byte[]>? ExtractFile(byte[] data)
    {
      var stream = new MemoryStream(data, false);
      var reader = new BinaryReader(stream);
      
      string path = null;
      byte[] contents = null;
      
      while(true)
      {
        var pair = ReadObject(reader);
        
        if(pair.Key == PlgxfEOF) break;
        else if(pair.Key == PlgxfPath)
          path = StrUtil.Utf8.GetString(pair.Value);
        else if(pair.Key == PlgxfData)
          contents = pair.Value;
        else { Debug.Assert(false); }
      }
      
      reader.Close();
      stream.Close();
      
      if (!string.IsNullOrEmpty (path) && contents != null) {
        byte[] pbDecompressed = MemUtil.Decompress(contents);
        return new KeyValuePair<string, byte[]> (path, pbDecompressed);
      }
      
      Debug.Assert (false);
      return null;
    }

    /// <summary>
    /// Extracts the file contents to destDir.
    /// </summary>
    /// <param name="contents">Contents to extract (from Files property).</param>
    /// <param name="destDir">Destination directory to store file.</param>
    public static void ExtractFile(byte[] contents, string destDir) {
      
      string path = null;
      
      string tempFile =
        UrlUtil.EnsureTerminatingSeparator (destDir, false) +
          UrlUtil.ConvertSeparators (path);
      
      string tempDir = UrlUtil.GetFileDirectory (tempFile, false, true);
      if (!Directory.Exists (tempDir)) {
        Directory.CreateDirectory (tempDir);
      }
      
      byte[] decompressedData = MemUtil.Decompress (contents);
      File.WriteAllBytes (tempFile, decompressedData);     
    }

    public override string ToString ()
    {
      return ToString (false);
    }

    public string ToString(bool verbose)
    {
      if (!verbose) {
        return BaseFileName;
      }

      var builder = new StringBuilder ();
      builder.AppendFormat ("File Format Version:          {0}\n", Version >> 16);
      builder.AppendFormat ("UUID:                         {0}\n",
                            FileUuid.ToHexString ());
      builder.AppendFormat ("Base File Name:               {0}\n", BaseFileName);
      builder.AppendFormat ("Creation Time:                {0}\n", CreationTime);
      builder.AppendFormat ("Generator Name:               {0}\n", GeneratorName);
      builder.AppendFormat ("Generator Version:            {0}\n",
                            StrUtil.VersionToString (GeneratorVersion));
      builder.AppendFormat ("Prerequsite KeePass Version:  {0}\n",
                            PrereqKP.HasValue ?
                            StrUtil.VersionToString (PrereqKP.Value) : none);
      builder.AppendFormat ("Prerequsite .NET Version:     {0}\n",
                            PrereqNet.HasValue ? 
                            StrUtil.VersionToString (PrereqNet.Value) : none);
      builder.AppendFormat ("Prerequsite Operating System: {0}\n",
                            PrereqOS != null ? PrereqOS : none);
      builder.AppendFormat ("Prerequsite Pointer size:     {0}\n",
                            PrereqPtr.HasValue ? PrereqPtr.Value.ToString () : none);
      builder.AppendLine ();
      builder.AppendLine ("Pre-build Command:");
      builder.AppendLine (BuildPre != null ? BuildPre : none);
      builder.AppendLine ("Post-build Command:");
      builder.AppendLine (BuildPost != null ? BuildPost : none);
      builder.AppendLine ();
      builder.AppendLine ("Files:");
      foreach (var file in Files) {
        // TODO - make bytes more readable with k or M
        builder.AppendFormat ("{0} ({1} bytes)\n", file.Key, file.Value.Length);
      }
      // remove last newline
      builder.Remove (builder.Length - 1, 1);
      return builder.ToString ();
    }
    /// <summary>
    /// Adds the file from disk.
    /// </summary>
    /// <param name="sourceFile">
    /// The path of the source file located on disk
    /// </param>
    /// <param name="destinationFile">
    /// The relitive path of destination file stored in the plgx
    /// </param>
    public void AddFileFromDisk(string sourceFile, string destinationFile)
    {
      sourceFile = Path.GetFullPath (sourceFile);
      var data = File.ReadAllBytes (sourceFile);
      Files.Add (destinationFile, data);      
    }
  }
}

