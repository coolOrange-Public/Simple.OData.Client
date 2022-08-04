using System;

namespace Simple.OData.Client
{
    public class ODataOrderByColumn : IEquatable<ODataOrderByColumn>
    {
        public string Name { get; private set; }
        public bool Descending { get; private set; }

        public ODataOrderByColumn(string name, bool descending)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException(string.Format((string)"Parameter {0} should not be null or empty.", "name"), "name");
            Name = name;
            Descending = descending;
        }

        public bool Equals(ODataOrderByColumn other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Descending == other.Descending;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ODataOrderByColumn) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ Descending.GetHashCode();
            }
        }
    }
}