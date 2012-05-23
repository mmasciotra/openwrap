﻿namespace OpenWrap.PackageModel.Parsers
{
    public class SingleSemanticVersionValue : SingleValue<SemanticVersion>
    {
        public SingleSemanticVersionValue(PackageDescriptorEntryCollection entries, string name)
                : base(
            entries,
            name, 
            x => x != null ? x.ToString() : null, 
            SemanticVersion.TryParseExact)
        {
        }
        public static SingleSemanticVersionValue New(PackageDescriptorEntryCollection entries, string name, SemanticVersion defaultVal)
        {
            return new SingleSemanticVersionValue(entries, name);
        }

    }
}