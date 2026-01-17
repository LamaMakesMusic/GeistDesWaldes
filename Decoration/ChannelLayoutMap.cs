using System;
using System.Text;

namespace GeistDesWaldes.Decoration;

[Serializable]
public class ChannelLayoutMap
{
    public enum LayoutTargetOption
    {
        ChannelName = 0,
        ChannelTopic = 1
    }

    public int Index;

    public LayoutTargetOption LayoutTarget;
    public string Value;

    public ChannelLayoutMap()
    {
    }

    public ChannelLayoutMap(int index, string identifier, LayoutTargetOption target)
    {
        Index = index;
        Value = identifier;
        LayoutTarget = target;

        EnsureFormat();
    }

    public void EnsureFormat()
    {
        byte[] bytes = Encoding.Unicode.GetBytes(string.IsNullOrEmpty(Value) ? "" : Value.Trim());
        Value = bytes != null ? Encoding.Unicode.GetString(bytes) : "";
    }

    public override string ToString()
    {
        return $"{Index} >> {Value}";
    }
}