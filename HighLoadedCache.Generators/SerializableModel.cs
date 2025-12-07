using System.Collections.Generic;

namespace HighLoadedCache.Generators
{
    internal sealed class SerializableProperty
    {
        public string Name { get; }
        public string TypeName { get; }

        public SerializableProperty(string name, string typeName)
        {
            Name = name;
            TypeName = typeName;
        }
    }

    internal sealed class SerializableType
    {
        public string Namespace { get; }
        public string TypeName { get; }
        public IReadOnlyList<SerializableProperty> Properties { get; }

        public SerializableType(string @namespace, string typeName, IReadOnlyList<SerializableProperty> properties)
        {
            Namespace = @namespace;
            TypeName = typeName;
            Properties = properties;
        }
    }
}