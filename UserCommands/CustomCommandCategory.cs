using System;
using System.Collections.Generic;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.UserCommands;

[Serializable]
public class CustomCommandCategory
{
    public float CategoryCooldownInSeconds;
    public List<string> Commands;
    public bool Locked;
    public string Name;
    public int PriceTag;

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