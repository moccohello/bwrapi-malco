namespace Malco.Application.Contracts
{
    internal readonly struct SessionId : System.IEquatable<SessionId>
    {
        public SessionId(string epoch, long generation)
        {
            Epoch = epoch ?? string.Empty;
            Generation = generation;
        }

        public string Epoch { get; }
        public long Generation { get; }

        public bool Equals(SessionId other)
        {
            return Generation == other.Generation &&
                   string.Equals(Epoch, other.Epoch, System.StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SessionId && Equals((SessionId)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (System.StringComparer.Ordinal.GetHashCode(Epoch ?? string.Empty) * 397) ^ Generation.GetHashCode();
            }
        }

        public static bool operator ==(SessionId left, SessionId right) => left.Equals(right);
        public static bool operator !=(SessionId left, SessionId right) => !left.Equals(right);
    }

    // A revision is comparable only inside one session. This prevents a
    // newer revision from an old game session being treated as current.
    internal readonly struct ChannelVersion : System.IEquatable<ChannelVersion>
    {
        public ChannelVersion(SessionId session, long revision)
        {
            Session = session;
            Revision = revision;
        }

        public SessionId Session { get; }
        public long Revision { get; }

        public bool IsSameSession(ChannelVersion other) => Session == other.Session;

        public bool IsOlderThan(ChannelVersion other)
        {
            return IsSameSession(other) && Revision < other.Revision;
        }

        public bool IsAtLeast(ChannelVersion other)
        {
            return IsSameSession(other) && Revision >= other.Revision;
        }

        public bool Equals(ChannelVersion other)
        {
            return Session == other.Session && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is ChannelVersion && Equals((ChannelVersion)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Session.GetHashCode() * 397) ^ Revision.GetHashCode();
            }
        }

        public static bool operator ==(ChannelVersion left, ChannelVersion right) => left.Equals(right);
        public static bool operator !=(ChannelVersion left, ChannelVersion right) => !left.Equals(right);
    }

    internal readonly struct AcquisitionSequence
    {
        public AcquisitionSequence(long value)
        {
            Value = value;
        }

        public long Value { get; }
    }

    internal readonly struct ContentRevision
    {
        public ContentRevision(long value)
        {
            Value = value;
        }

        public long Value { get; }
    }

    internal readonly struct ProjectionPresentationRevision
    {
        public ProjectionPresentationRevision(long value)
        {
            Value = value;
        }

        public long Value { get; }
    }

}
