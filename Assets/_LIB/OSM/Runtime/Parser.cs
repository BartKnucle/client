using System.Collections.Generic;
using FunkySheep.JSON;

namespace FunkySheep.OSM
{
  public static class Parser 
  {
    public static Data Parse(JSONNode jsonData)
    {
      JSONArray elements = jsonData["elements"].AsArray;
      Data data = new Data();

      for (int i = 0; i < elements.Count; i++)
      {
        data.AddElement(elements[i]);
      }

      return data;
    }

    public static Data Parse(string textData)
    {
      JSONNode jsonData = JSONNode.Parse(textData);
      return Parse(jsonData);
    }

    public static Data Parse(byte[] rawData)
    {
      string textData = System.Text.Encoding.UTF8.GetString(rawData);
      return Parse(textData);
    }
  }  
}
