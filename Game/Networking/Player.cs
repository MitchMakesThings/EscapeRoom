using Godot;
using System;

public class Player : Godot.Object
{
    public string Name { get; set; }
    public int NetworkId { get; set; }
    public Color Color { get; set; } // TODO - use this for setting character colour

    public Godot.Collections.Dictionary<string, object> ToGodotDictionary() {
        Godot.Collections.Dictionary<string, object> returnVal = new Godot.Collections.Dictionary<string, object>();
        returnVal.Add("Name", Name);
        returnVal.Add("NetworkId", NetworkId);
        returnVal.Add("Color", Color);

        return returnVal;
    }

    public void PopulateFromGodotDictionary(Godot.Collections.Dictionary<string, object> dict) {
        object data;
        dict.TryGetValue("Name", out data);
        Name = (string)data;

        dict.TryGetValue("NetworkId", out data);
        NetworkId = (int)data;

        dict.TryGetValue("Color", out data);
        Color = (Color)data;

    }
}
