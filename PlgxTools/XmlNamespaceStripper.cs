using System.Xml;

namespace KeePassPluginDevTools.PlgxTools
{
  /// <summary>
  /// Xml namespace stripper.
  /// </summary>
  /// <remarks>
  /// Copied from http://forums.asp.net/post/set/43/1483243/3467837
  /// </remarks>
  class XmlNamespaceStripper
  {
    const string xmlnsNs = "http://www.w3.org/2000/xmlns/";
    const string defaultNs = "xmlns";
        
    public static XmlDocument StripNamespace(XmlDocument input)
    {
      var output = new XmlDocument();
      output.PreserveWhitespace = true;
      foreach (XmlNode child in input.ChildNodes)
      {
        output.AppendChild(StripNamespace(child, output));
      }
      return output;
    }    
    
    static XmlNode StripNamespace(XmlNode inputNode, XmlDocument output)
    {
      XmlNode outputNode = output.CreateNode(inputNode.NodeType, inputNode.LocalName, null);      
      
      // copy attributes, stripping namespaces
      if (inputNode.Attributes != null)
      {
        foreach (XmlAttribute inputAttribute in inputNode.Attributes)
        {
          if (!(inputAttribute.NamespaceURI == xmlnsNs || inputAttribute.LocalName == defaultNs))
          {
            XmlAttribute outputAttribute = output.CreateAttribute(inputAttribute.LocalName);
            outputAttribute.Value = inputAttribute.Value;
            outputNode.Attributes.Append(outputAttribute);
          }
        }
      }      
      
      // copy child nodes, stripping namespaces
      foreach (XmlNode childNode in inputNode.ChildNodes)
      {
        outputNode.AppendChild(StripNamespace(childNode, output));
      }      
      
      // copy value for nodes without children
      if (inputNode.Value != null)
      {
        outputNode.Value = inputNode.Value;
      }      
      
      return outputNode;
    }
  }
}