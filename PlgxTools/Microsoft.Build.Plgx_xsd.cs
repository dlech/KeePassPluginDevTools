using System;
using System.Xml.Serialization;


namespace KeePassPluginDevTools.PlgxTools
{
  public static class PlgxConfigurationExtensions
  {
    public static string Test(this string test)
    {

      return test;
    }

    public static string GetValue(this PlgxConfigurationPrerequisitesOS version)
    {
      return GetXmlEnumAttribute (version) ?? version.ToString ();
    }

    public static string GetValue(this PlgxConfigurationPrerequisitesPointerSize version)
    {
      return GetXmlEnumAttribute (version) ?? version.ToString ();
    }

    private static string GetXmlEnumAttribute(Enum enumValue)
    {
      Type objType = enumValue.GetType ();
      var field = objType.GetField (enumValue.ToString ());
      var attributes = field.GetCustomAttributes (typeof(XmlEnumAttribute), true);
      foreach (var attribute in attributes) {
        return (attribute as XmlEnumAttribute).Name;
      }
      return null;
    }
  }
}

