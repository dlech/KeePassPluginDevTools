using System;

namespace KeePassPluginDevTools.PlgxTools
{
  public class PlgxException : Exception
  {
    private string mMessage;

    public override string Message
    {
      get { return mMessage; }
    }

    public PlgxException(string aMessage)
    {
      if (aMessage == null) {
        throw new ArgumentNullException ("aMessage");
      }
      mMessage = aMessage;
    }
  }
}

