using System;

namespace DotNetPlease.Annotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ArgumentAttribute : Attribute
    {
        public int Index { get; }
        public string Description { get; }

        public ArgumentAttribute(int index, string description)
        {
            Index = index;
            Description = description;
        }
    }
}