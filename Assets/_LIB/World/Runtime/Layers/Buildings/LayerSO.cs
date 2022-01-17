using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using SimpleJSON;

namespace FunkySheep.World.Buildings
{
  [CreateAssetMenu(menuName = "FunkySheep/World/Layers/Buildings")]
  public class LayerSO : FunkySheep.World.LayerSO
  {
    public string cacheRelativePath = "/world/buildings/";
    string path;
    public FunkySheep.Types.String url;
    public List<Way> ways = new List<Way>();
    public List<Relation> relations = new List<Relation>();
    public FunkySheep.Types.Vector3 initialMercatorPosition;
    public GameObject buildingPrefab;
    private void OnEnable() {
      path = Application.persistentDataPath + cacheRelativePath;
      //Create the cache directory
      if (!Directory.Exists(path))
      {
        Directory.CreateDirectory(path);
      }
    }
    public override Tile AddTile(FunkySheep.World.Manager world, Layer layer)
    {
      Tile tile = new Tile(world, layer);
      string url = InterpolatedUrl(tile.gpsBoundaries);
      layer.StartCoroutine(Load(url, (data) => {
          ParseTileData(tile, layer, data["elements"].AsArray);
      }));
      return tile;
    }

    /// <summary>
    /// Interpolate the url inserting the boundaries and the types of OSM data to download
    /// </summary>
    /// <param boundaries="boundaries">The gps boundaries to download in</param>
    /// <returns>The interpolated Url</returns>
    public string InterpolatedUrl(double[] boundaries)
    {
        string [] parameters = new string[5];
        string [] parametersNames = new string[5];

        parameters[0] = boundaries[0].ToString().Replace(',', '.');
        parametersNames[0] = "startLatitude";

        parameters[1] = boundaries[1].ToString().Replace(',', '.');
        parametersNames[1] = "startLongitude";

        parameters[2] = boundaries[2].ToString().Replace(',', '.');
        parametersNames[2] = "endLatitude";

        parameters[3] = boundaries[3].ToString().Replace(',', '.');
        parametersNames[3] = "endLongitude";

        return url.Interpolate(parameters, parametersNames);
    }

    /// <summary>
    /// Clear the buildings cache
    /// </summary>
    public void ClearCache()
    {
      foreach (string file in Directory.GetFiles(path))
      {
          File.Delete(file);
      }
    }

    /// <summary>
    /// Load a tile either from the disk or from the internet
    /// </summary>
    public IEnumerator Load(string url, Action<JSONNode> Callback)
    {
        string hash = FunkySheep.Crypto.Hash(url);
        if (File.Exists(path + hash))
        {
            LoadFromDisk(path + hash, Callback);
            yield break;
          } else {
            yield return DownLoad(url, path + hash, Callback);
          }
    }

        /// <summary>
        /// Load tile file from the disk
        /// </summary>
    public void LoadFromDisk(string path, Action<JSONNode> Callback)
    {
      JSONNode data = JSON.Parse(File.ReadAllText(path));
      Callback(data);
    }

    /// <summary>
    /// Download the tile
    /// </summary>
    /// <param name="callback">The callback to be run after download complete</param>
    /// <returns></returns>
    public IEnumerator DownLoad(string url, string path, Action<JSONNode> Callback) {
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
            if(request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(request.error);
            }
            else
            {
                JSONNode data = JSON.Parse(request.downloadHandler.text);
                File.WriteAllText(path, request.downloadHandler.text);
                Callback(data);
            }
    }

    /// <summary>
    /// Parse the data from the file
    /// </summary>
    public void ParseTileData(Tile tile, Layer layer, JSONArray elements)
    {
        // Add all the nodes first
        for (int i = 0; i < elements.Count; i++)
        {
            switch ((string)elements[i]["type"])
            {
                case "way":
                    AddWay(tile, layer, elements[i]);
                    break;
                case "relation":
                    AddRelation(tile, layer, elements[i]);
                    break;
                default:
                    break;
            }
        }
    }

    public Way AddWay(Tile tile, Layer layer, JSONNode wayJSON)
    {
        Way way = new Way(wayJSON["id"]);
        // Add the node id to the nodes list
        JSONArray points = wayJSON["geometry"].AsArray;

        for (int j = 0; j < points.Count; j++)
        {
            way.points.Add(new Point(points[j]["lat"], points[j]["lon"], this.initialMercatorPosition.Value));
        }

        JSONObject tags = wayJSON["tags"].AsObject;

        foreach (KeyValuePair<string, JSONNode> tag in (JSONObject)tags)
        {
            way.tags.Add(new Tag(tag.Key, tag.Value));
        }
        Build(way, layer);
        ways.Add(way);
        
        return way;
    }

    public Relation AddRelation(Tile tile, Layer layer, JSONNode relationJSON)
    {
        Relation relation = new Relation(relationJSON["id"]);

        JSONArray members = relationJSON["members"].AsArray;

        for (int j = 0; j < members.Count; j++)
        {
            Way way = ways.Find(way => way.id == members[j]["ref"]);
            if (way == null) {
              way = new Way(members[j]["ref"]);
              JSONArray points = members[j]["geometry"].AsArray;
              for (int k = 0; k < points.Count; k++)
              {
                  way.points.Add(new Point(points[k]["lat"], points[k]["lon"], this.initialMercatorPosition.Value));
              }
            }
            Build(way, layer);
            relation.ways.Add(way);
        }

        JSONObject tags = relationJSON["tags"].AsObject;

        foreach (KeyValuePair<string, JSONNode> tag in (JSONObject)tags)
        {
            relation.tags.Add(new Tag(tag.Key, tag.Value));
        }
        relations.Add(relation);
        return relation;
    }

    /// <summary>
    /// Build the 3D Object
    /// </summary>
    /// <param name="way"></param>
    public void Build(Way way, Layer layer)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.points = new Vector2[way.points.Count];
        building.id = way.id.ToString();

        for (int i = 0; i < way.points.Count; i++)
        {
          building.points[i] = way.points[i].position;
        }

        GameObject go = Instantiate(buildingPrefab);
        building.Init();
        go.name = building.id;
        go.transform.parent = layer.transform;
        go.transform.position = new Vector3(building.position.x, 0, building.position.y);
        go.GetComponent<Manager>().Create(building);
    }
  }
}