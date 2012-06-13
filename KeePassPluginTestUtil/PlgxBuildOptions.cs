using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeePassPluginTestUtil
{
    public struct PlgxBuildOptions
    {
        public string projectPath;
        public string keepassVersion;
        public string dotnetVersion;
        public string os;
        public string pointerSize;
        public string preBuild;
        public string postBuild;
    }
}
