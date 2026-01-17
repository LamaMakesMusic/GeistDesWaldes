using System;
using System.Collections.Generic;

namespace GeistDesWaldes.UserCommands;

[Serializable]
public class CustomCommandDictionary
{
    public List<CustomCommandCategory> Categories;
    public List<CustomCommand> Commands;

    public CustomCommandDictionary()
    {
        Commands = new List<CustomCommand>();
        Categories = new List<CustomCommandCategory>();
    }
}