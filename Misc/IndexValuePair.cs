using System;

namespace GeistDesWaldes.Misc;

[Serializable]
public class IndexValuePair
{
    public int Index;
    public string Value;


    public IndexValuePair()
    {
    }

    public IndexValuePair(int index, string value)
    {
        Index = index;
        Value = value;
    }
}