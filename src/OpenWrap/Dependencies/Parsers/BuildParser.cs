﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenWrap.Dependencies.Parsers
{
    /// <summary>
    /// parses the build instruction from the wrap descriptor
    /// </summary>
    public class BuildParser : AbstractDescriptorParser
    {
        public BuildParser() : base("build")
        {
        }
        protected override void ParseContent(string content, WrapDescriptor descriptor)
        {
            descriptor.BuildCommand = content;
        }

        protected override IEnumerable<string> WriteContent(WrapDescriptor descriptor)
        {
            if (descriptor.BuildCommand != null)
                yield return descriptor.BuildCommand;
        }
    }
}
