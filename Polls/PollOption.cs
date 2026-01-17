using System;

namespace GeistDesWaldes.Polls;

[Serializable]
public class PollOption
{
    public string Identifier;
    public int IdentifierHash;

    public string Name;

    public int Votes;

    public PollOption()
    {
    }

    public PollOption(string name, string identifier)
    {
        SetIdentifier(identifier);

        Name = name;
    }

    public void SetIdentifier(string identifier)
    {
        Identifier = identifier.ToLower();
        IdentifierHash = Identifier.GetHashCode();
    }

    public override string ToString()
    {
        string shortName = Name.Length < 25 ? Name : $"{Name.Substring(0, 19)}[...]";

        return $"[{Identifier,-3}] {shortName,-24} ({Votes,-6:00})";
    }
}