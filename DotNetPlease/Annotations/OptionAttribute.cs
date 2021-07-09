using System;

namespace DotNetPlease.Annotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class OptionAttribute : Attribute
    {
        public string Alias { get; }
        public string Description { get; }

        public OptionAttribute(string alias, string description)
        {
            Alias = alias;
            Description = description;
        }
    }
}