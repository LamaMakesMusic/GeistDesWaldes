using Discord;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using System;
using System.Collections.Generic;

namespace GeistDesWaldes.UserCommands
{
    [Serializable]
    public class CustomCommandCategory
    {
        public string Name;
        public List<string> Commands;
        public float CategoryCooldownInSeconds;
        public int PriceTag;
        public bool Locked;

        public CustomCommandCategory()
        {
            Commands = new List<string>();
        }
        public CustomCommandCategory(string name)
        {
            Commands = new List<string>();
            Name = name;
        }

        public override string ToString()
        {
            return $" {Name} | {GetCostsString()}";
        }
        public string GetCostsString()
        {
            return $"{(Locked ? EmojiDictionary.GetEmoji(EmojiDictionary.LOCKED) : EmojiDictionary.GetEmoji(EmojiDictionary.UNLOCKED))} | {Utility.CreateCostsString(CategoryCooldownInSeconds, PriceTag)}";
        }
    }
}
