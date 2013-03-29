using System;
using System.Windows.Forms;
using KeePass.Plugins;

namespace SamplePlugin
{
  public sealed class SamplePluginExt : Plugin
  {
    private IPluginHost mPluginHost;

    public override bool Initialize(IPluginHost host)
    {
      mPluginHost = host;
      MessageBox.Show("Sample Plugin Initialized!");
      return true;
    }

    public override void Terminate()
    {
      MessageBox.Show("Sample Plugin Terminated!");
    }
  }
}
