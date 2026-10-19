using System;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Attribute to mark a class as an MCP tool for automatic registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class McpToolAttribute : Attribute
    {
        public string Name { get; }

        public McpToolAttribute(string name)
        {
            Name = name;
        }
    }
}
