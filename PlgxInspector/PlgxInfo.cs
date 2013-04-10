using System;
using KeePassLib;
using System.Collections.Generic;

namespace PlgxInspector
{
  public class PlgxInfo
  {
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
    public ulong PrereqKP { get; set; }

    /// <summary>
    /// Gets or sets the prerequsite .NET version.
    /// </summary>
    public ulong PrereqNet { get; set; }

    /// <summary>
    /// Gets or sets the prerequsite operating system
    /// </summary>
    public string PrereqOS { get; set; }

    /// <summary>
    /// Gets or sets the prerequsite pointer size
    /// </summary>
    public uint PrereqPtr { get; set; }

    /// <summary>
    /// Gets or sets the pre-build command
    /// </summary>
    public string BuildPre { get; set; }

    /// <summary>
    /// Gets or sets the post-build command
    /// </summary>
    public string BuildPost { get; set; }

    public IDictionary<string, byte[]> Files { get; set; }

    public PlgxInfo ()
    {
      Files = new Dictionary<string, byte[]> ();
    }
  }
}

