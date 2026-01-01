using System;
using System.Text;

namespace GeistDesWaldes.Decoration
{
    [Serializable]
    public class ChannelLayoutMap
    {
        public int Index;
        public string Value;

        public LayoutTargetOption LayoutTarget;

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


        public enum LayoutTargetOption
        {
            ChannelName = 0,
            ChannelTopic = 1
        }

        public override string ToString()
        {
            return $"{Index} >> {Value}";
        }
    }
}
