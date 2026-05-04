using System;
using System.Collections.Generic;
using Newtonsoft.Json; // Ensure you have the Newtonsoft Json package

[Serializable]
public class ActionConfig
{
    [JsonProperty("actions")] // Forces lowercase in the JSON payload
    public List<string> actions = new List<string>();

    [JsonProperty("characters")]
    public List<Types.Character> characters = new List<Types.Character>();

    [JsonProperty("objects")]
    public List<Types.Object> objects = new List<Types.Object>();

    [JsonProperty("classification")]
    public string classification = "multistep";

    [JsonProperty("context_level")] // The API usually expects snake_case
    public int contextLevel;

    [JsonProperty("current_attention_object")]
    public string currentAttentionObject;

    [Serializable]
    public static class Types
    {
        [Serializable]
        public struct Character
        {
            [JsonProperty("name")]
            public string name;
            [JsonProperty("bio")]
            public string bio;
        }

        [Serializable]
        public struct Object
        {
            [JsonProperty("name")]
            public string name;
            [JsonProperty("description")]
            public string description;
        }
    }
}